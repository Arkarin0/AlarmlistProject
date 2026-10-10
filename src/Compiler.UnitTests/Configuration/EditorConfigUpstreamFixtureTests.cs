// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;

namespace Alarmlist.Core.UnitTests.Configuration
{
    public sealed class EditorConfigUpstreamFixtureTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistCorpusCache", Guid.NewGuid().ToString("N"));
        private string Cache => Path.Combine(_directory, "corpus.zip");
        private static readonly byte[] s_payload = { 1, 2, 3 };
        private static string Hash
        {
            get
            {
                using (var algorithm = SHA256.Create())
                    return BitConverter.ToString(algorithm.ComputeHash(s_payload)).Replace("-", "");
            }
        }

        [Fact]
        public async Task DownloadsMissingCorpusThenReusesItWithoutNetwork()
        {
            int downloads = 0;
            Assert.Equal(s_payload, await EditorConfigUpstreamFixture.GetArchiveAsync(Cache, Hash,
                () => { downloads++; return Task.FromResult(s_payload); }));
            Assert.Equal(s_payload, await EditorConfigUpstreamFixture.GetArchiveAsync(Cache, Hash,
                () => throw new InvalidOperationException("A warm cache must work offline.")));
            Assert.Equal(1, downloads);
            Assert.Single(Directory.GetFiles(_directory));
        }

        [Fact]
        public async Task RejectsCorruptCachedDataWithoutDownloadingOrOverwritingIt()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllBytes(Cache, new byte[] { 9 });
            await Assert.ThrowsAsync<InvalidDataException>(() => EditorConfigUpstreamFixture.GetArchiveAsync(Cache, Hash,
                () => throw new Exception("The existing cache must be checked first.")));
            Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(Cache));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedOrInvalidDownloadsDoNotInstallACache(bool corrupt)
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                EditorConfigUpstreamFixture.GetArchiveAsync(Cache, Hash, () => corrupt
                    ? Task.FromResult(new byte[] { 9 }) : throw new IOException("Unavailable")));
            Assert.Contains(Cache, error.Message);
            Assert.False(File.Exists(Cache));
            Assert.False(Directory.Exists(_directory));
        }

        [Fact]
        public async Task ConcurrentDownloadKeepsTheVerifiedWinnerAndRemovesItsTemporaryFile()
        {
            Assert.Equal(s_payload, await EditorConfigUpstreamFixture.GetArchiveAsync(Cache, Hash, () =>
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllBytes(Cache, s_payload);
                return Task.FromResult(s_payload);
            }));
            Assert.Single(Directory.GetFiles(_directory));
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
