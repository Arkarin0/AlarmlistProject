// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Diagnostics.Runtime;

namespace Alarmlist.Tools.VisualStudioHeapProbe
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.WriteLine("VisualStudioHeapProbe <PID> <expected start UTC ticks> <new JSON output path>");
                return 0;
            }
            try
            {
                if (args.Length != 3 || !int.TryParse(args[0], out int processId)
                    || !long.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expectedStart))
                    throw new ArgumentException("Use eng/capture-visualstudio-heap.ps1 to select a running test process.");
                string output = Path.GetFullPath(args[2]);
                if (File.Exists(output)) throw new IOException("Refusing to overwrite an existing heap report.");
                using Process process = Process.GetProcessById(processId);
                if ((!process.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase)
                    && !process.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase)
                    && !process.ProcessName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase))
                    || process.StartTime.ToUniversalTime().Ticks != expectedStart)
                    throw new InvalidOperationException("The selected process is not the expected IDE/test host/driver instance.");
                var timer = Stopwatch.StartNew();
                using DataTarget target = DataTarget.CreateSnapshotAndAttach(processId);
                if (process.HasExited) throw new InvalidOperationException("The selected process exited during capture.");
                var results = new List<object>();
                foreach (ClrInfo info in target.ClrVersions)
                {
                    using ClrRuntime runtime = info.CreateRuntime();
                    ClrHeap heap = runtime.Heap;
                    if (!heap.CanWalkHeap) throw new InvalidOperationException("Heap cannot be walked.");
                    var all = new Dictionary<string, Stats>();
                    var objects = new Dictionary<ulong, ClrObject>();
                    foreach (ClrObject obj in heap.EnumerateObjects())
                    {
                        if (!obj.IsValid || obj.IsFree) continue;
                        objects[obj.Address] = obj;
                        Add(all, obj);
                    }
                    var parents = new Dictionary<ulong, ulong>();
                    var roots = new Dictionary<ulong, string>();
                    var queue = new Queue<ClrObject>();
                    foreach (ClrRoot root in heap.EnumerateRoots())
                    {
                        ClrObject obj = root.Object;
                        if (!obj.IsValid || obj.IsFree || !parents.TryAdd(obj.Address, 0)) continue;
                        roots[obj.Address] = root.RootKind.ToString();
                        queue.Enqueue(obj);
                    }
                    var live = new Dictionary<string, Stats>();
                    while (queue.Count > 0)
                    {
                        ClrObject obj = queue.Dequeue();
                        Add(live, obj);
                        foreach (ClrObject next in obj.EnumerateReferences(true, true))
                            if (next.IsValid && !next.IsFree && parents.TryAdd(next.Address, obj.Address)) queue.Enqueue(next);
                    }
                    ClrObject[] watched = objects.Values.Where(obj => obj.Type?.Name is
                        "Alarmlist.VisualStudio.Editor.AlmxEditorPane" or "Alarmlist.VisualStudio.UI.EditFileControlViewModel" or
                        "Alarmlist.VisualStudio.Editor.VsDesignerAnalysis" or "Alarmlist.VisualStudio.Analysis.ProjectAnalysisState").ToArray();
                    var paths = watched.Where(obj => parents.ContainsKey(obj.Address)).GroupBy(obj => obj.Type.Name)
                        .SelectMany(group => group.Take(3)).Select(obj =>
                        {
                            var path = new List<string>();
                            ulong current = obj.Address;
                            while (current != 0 && path.Count < 150)
                            {
                                ClrObject item = heap.GetObject(current);
                                path.Add($"{current:x}: {item.Type?.Name}" + (roots.TryGetValue(current, out string kind) ? $" [ROOT {kind}]" : ""));
                                current = parents[current];
                            }
                            path.Reverse();
                            return path;
                        }).ToArray();
                    results.Add(new
                    {
                        Runtime = info.Version.ToString(), DomainCount = runtime.AppDomains.Length,
                        Threads = runtime.Threads.Where(thread => thread.IsAlive).Select(thread => new
                        {
                            thread.OSThreadId, thread.ManagedThreadId,
                            Frames = thread.EnumerateStackTrace().Take(80).Select(frame => frame.ToString()).ToArray()
                        }).ToArray(),
                        TotalObjects = objects.Count, TotalBytes = all.Values.Sum(stats => stats.Bytes),
                        ReachableObjects = parents.Count, ReachableBytes = live.Values.Sum(stats => stats.Bytes),
                        Types = all.Select(pair => new
                        {
                            Type = pair.Key, pair.Value.Count, pair.Value.Bytes,
                            ReachableCount = live.GetValueOrDefault(pair.Key)?.Count ?? 0,
                            ReachableBytes = live.GetValueOrDefault(pair.Key)?.Bytes ?? 0
                        }).OrderByDescending(item => item.ReachableBytes).ToArray(),
                        Watched = watched.Select(obj => new
                        {
                            Type = obj.Type?.Name, Address = obj.Address.ToString("x"), Reachable = parents.ContainsKey(obj.Address),
                            Disposed = obj.TryReadField("_disposed", out bool disposed) ? (bool?)disposed : null
                        }).ToArray(), Paths = paths
                    });
                    Console.WriteLine($"Reachable: {parents.Count} objects, {live.Values.Sum(stats => stats.Bytes) / 1048576.0:F1} MiB.");
                }
                if (results.Count == 0) throw new InvalidOperationException("No managed runtime was available for inspection.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using FileStream stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(stream, new
                {
                    SchemaVersion = 1, ProcessId = processId, ProcessStartUtcTicks = expectedStart,
                    CapturedUtc = DateTime.UtcNow, ElapsedSeconds = timer.Elapsed.TotalSeconds, Runtimes = results
                }, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(output);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void Add(Dictionary<string, Stats> values, ClrObject obj)
        {
            string name = obj.Type?.Name ?? "<unknown>";
            if (!values.TryGetValue(name, out Stats stats)) values[name] = stats = new Stats();
            stats.Count++;
            stats.Bytes += (long)obj.Size;
        }

        private sealed class Stats
        {
            public long Count;
            public long Bytes;
        }
    }
}
