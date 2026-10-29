// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Alarmlist.Extensibility
{
    internal sealed class CapturedAssemblyScope : IDisposable
    {
        private readonly Dictionary<string, byte[]> _images;
        private readonly string _description;
#if NET8_0_OR_GREATER
        private readonly CapturedAssemblyLoadContext _context;
#else
        private static readonly object s_cacheGate = new object();
        private static readonly Dictionary<string, Assembly> s_cache = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> s_imageIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Assembly> _owned = new HashSet<Assembly>();
        private readonly string _identity;
#endif
        internal CapturedAssemblyScope(IEnumerable<KeyValuePair<string, byte[]>> images, string description)
        {
            _description = description;
            _images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in images)
            {
                string name = Path.GetFileNameWithoutExtension(image.Key);
                if (name == typeof(CapturedAssemblyScope).Assembly.GetName().Name) continue;
                if (_images.ContainsKey(name)) throw new ArgumentException("Duplicate " + _description + " dependency name '" + name + "'.");
                _images.Add(name, (byte[])image.Value.Clone());
            }
#if NET8_0_OR_GREATER
            _context = new CapturedAssemblyLoadContext(_images);
#else
            using (var hash = SHA256.Create())
            using (var contents = new MemoryStream())
            {
                var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in _images.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    byte[] name = Encoding.UTF8.GetBytes(pair.Key + "\0");
                    contents.Write(name, 0, name.Length);
                    byte[] digest = hash.ComputeHash(pair.Value);
                    identities.Add(pair.Key, Convert.ToBase64String(digest));
                    contents.Write(digest, 0, digest.Length);
                }
                _identity = Convert.ToBase64String(hash.ComputeHash(contents.ToArray()));
                // The desktop CLR can reuse bindings by assembly identity even for
                // byte-loaded copies. Never let a changed dependency run stale code.
                // VS renews its SDK domain; a desktop build must renew its node.
                lock (s_cacheGate)
                {
                    foreach (var pair in identities)
                        if (s_imageIdentities.TryGetValue(pair.Key, out string previous) && previous != pair.Value)
                            throw new NotSupportedException(_description + " assembly '" + pair.Key
                                + "' changed or conflicts with an assembly already captured in this .NET Framework process. Reload the analysis AppDomain or restart desktop MSBuild with /nodeReuse:false.");
                    foreach (var pair in identities) s_imageIdentities[pair.Key] = pair.Value;
                }
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
#endif
        }

        internal Assembly LoadRoot(string path) => Load(Path.GetFileNameWithoutExtension(path))
            ?? throw new FileNotFoundException("The " + _description + " image was not captured: " + path);

        private Assembly Load(string name)
        {
            if (!_images.TryGetValue(name, out byte[] image)) return null;
#if NET8_0_OR_GREATER
            using (var stream = new MemoryStream(image, false)) return _context.LoadFromStream(stream);
#else
            Assembly assembly;
            lock (s_cacheGate)
            {
                string key = _identity + ":" + name;
                if (!s_cache.TryGetValue(key, out assembly)) s_cache.Add(key, assembly = Assembly.Load(image));
            }
            lock (_owned) _owned.Add(assembly);
            return assembly;
#endif
        }

#if !NET8_0_OR_GREATER
        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            lock (_owned)
                if (args.RequestingAssembly == null || !_owned.Contains(args.RequestingAssembly)) return null;
            string name = new AssemblyName(args.Name).Name;
            if (name == typeof(CapturedAssemblyScope).Assembly.GetName().Name) return typeof(CapturedAssemblyScope).Assembly;
            return Load(name);
        }
#endif
        internal static bool IsRecoverable(Exception exception) => !(exception is OutOfMemoryException) && !(exception is StackOverflowException)
            && !(exception is AccessViolationException) && !(exception is AppDomainUnloadedException) && !(exception is ThreadAbortException);

        public void Dispose()
        {
            try
            {
#if NET8_0_OR_GREATER
                try { _context.Unload(); }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    // .NET 8 clears Unloading handlers and suppresses finalization
                    // before invoking callbacks. A thrown callback can therefore
                    // prevent native release; retry once and retain the failure.
                    try { _context.Unload(); }
                    catch (Exception retry) when (IsRecoverable(retry)) { }
                    throw;
                }
#else
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                lock (_owned) _owned.Clear();
#endif
            }
            finally { _images.Clear(); }
        }
    }

#if NET8_0_OR_GREATER
    internal sealed class CapturedAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, byte[]> _images;
        internal CapturedAssemblyLoadContext(Dictionary<string, byte[]> images) : base(isCollectible: true) { _images = images; }
        protected override Assembly Load(AssemblyName name)
        {
            if (name.Name == typeof(CapturedAssemblyScope).Assembly.GetName().Name) return typeof(CapturedAssemblyScope).Assembly;
            if (!_images.TryGetValue(name.Name, out byte[] image)) return null;
            using (var stream = new MemoryStream(image, false)) return LoadFromStream(stream);
        }
    }
#endif
}
