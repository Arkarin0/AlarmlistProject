// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Alarmlist.VisualStudio.Testing;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (arguments[0] == "fail") return 7;
        if (arguments[0] == "echo")
        {
            Console.WriteLine("CWD:" + Environment.CurrentDirectory);
            Console.WriteLine("ENV:" + Environment.GetEnvironmentVariable("ALARMLIST_PROCESS_PROBE"));
            foreach (string value in arguments.Skip(1)) Console.WriteLine("ARG:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            Console.WriteLine("CONSOLE:" + GetConsoleWindow().ToInt64());
            Console.WriteLine("COMMANDLINE:" + Marshal.PtrToStringUni(GetCommandLine()));
            return 0;
        }
        if (arguments[0] == "output")
        { Console.Write(new string('o', 131072)); Console.Error.Write(new string('e', 131072)); return 0; }
        if (arguments[0] == "wait")
        {
            using (var self = Process.GetCurrentProcess())
            {
                File.WriteAllText(arguments[1] + ".tmp", self.Id + "\n" + self.StartTime.ToUniversalTime().Ticks);
                File.Move(arguments[1] + ".tmp", arguments[1]);
            }
            Thread.Sleep(300000); return 0;
        }
        var start = Child("wait", arguments[1]);
        if (arguments[0] == "owner")
        {
            using (var child = OwnedTestProcess.Start(start)) { WaitForReceipt(arguments[1]); Thread.Sleep(300000); }
            return 0;
        }
        // Deliberately use ordinary Process.Start: inheritance must contain the
        // child before the parent can spawn it, without a second assignment.
        using (var child = Process.Start(start))
        {
            WaitForReceipt(arguments[1]);
            if (arguments[0] == "spawn-wait") Thread.Sleep(300000);
            return arguments[0] == "spawn-fail" ? 7 : 0;
        }
    }
    private static ProcessStartInfo Child(params string[] arguments)
    {
        string assembly = Assembly.GetExecutingAssembly().Location;
#if NETFRAMEWORK
        var start = new ProcessStartInfo(assembly);
#else
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
        arguments = new[] { assembly }.Concat(arguments).ToArray();
#endif
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.Arguments = string.Join(" ", arguments.Select(OwnedTestProcess.Quote));
        return start;
    }
    private static void WaitForReceipt(string path)
    {
        var timer = Stopwatch.StartNew();
        while (!File.Exists(path) && timer.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(10);
        if (!File.Exists(path)) throw new TimeoutException("The probe child did not start.");
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll", EntryPoint = "GetCommandLineW")] private static extern IntPtr GetCommandLine();
}
