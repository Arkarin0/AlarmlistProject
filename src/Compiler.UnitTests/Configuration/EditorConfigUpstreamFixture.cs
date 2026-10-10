// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public sealed class EditorConfigUpstreamFixture : IAsyncLifetime
    {
        internal const string Revision = "895b3a65d0d823dbd0acf2bc402376381995d1b1";
        private const string ArchiveHash = "5D05A34CF99F837E8EF9F5D56B32E054EEC49B4F81F70F7339CBA7FC78FC005F";
        internal Dictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public async Task InitializeAsync()
        {
            string repository = typeof(EditorConfigUpstreamFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item => item.Key == "RepositoryRoot").Value;
            string path = Path.Combine(repository, "artifacts", "testassets", "editorconfig", Revision + ".zip");
            byte[] bytes = await GetArchiveAsync(path, ArchiveHash, async () =>
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
                    return await client.GetByteArrayAsync("https://codeload.github.com/editorconfig/editorconfig-core-test/zip/" + Revision);
            });
            // Read data only. Nothing from upstream is executed or extracted to paths
            // supplied by the archive. Keep its license and provenance in the cache.
            using (var stream = new MemoryStream(bytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string prefix = "editorconfig-core-test-" + Revision + "/";
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) || entry.Name.Length == 0) continue;
                    using (var reader = new StreamReader(entry.Open()))
                        Files.Add(entry.FullName.Substring(prefix.Length), reader.ReadToEnd());
                }
            }
            Assert.Contains("LICENSE.txt", Files.Keys);
        }

        internal static async Task<byte[]> GetArchiveAsync(string path, string hash, Func<Task<byte[]>> download)
        {
            if (File.Exists(path)) return Validate(File.ReadAllBytes(path), hash, path);
            byte[] bytes;
            try { bytes = Validate(await download(), hash, path); }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Cannot prepare the pinned EditorConfig corpus. Allow HTTPS access to codeload.github.com "
                    + "or populate the verified cache at " + path + ".", exception);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path))
                {
                    // Another framework/test process may have finished the same download.
                    return Validate(File.ReadAllBytes(path), hash, path);
                }
                return bytes;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static byte[] Validate(byte[] bytes, string hash, string path)
        {
            using (var algorithm = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("EditorConfig corpus checksum mismatch; remove the invalid cache and retry: " + path);
            return bytes;
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
