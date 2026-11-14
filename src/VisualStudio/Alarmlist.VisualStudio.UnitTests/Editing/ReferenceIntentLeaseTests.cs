// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;
using Alarmlist.VisualStudio.Editor;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class ReferenceIntentLeaseTests
    {
        [Fact]
        public void RenameRetainsSourceIntentAfterReferencesReleaseAndReleasesItExactlyOnce()
        {
            int releases = 0;
            using (var context = new CancellationTokenSource())
            {
                var references = new ReferenceIntentLease(() => true, context.Token, () => releases++);
                var rename = references.Retain();
                references.Release(); Assert.True(rename.IsCurrent); Assert.Equal(0, releases);
                context.Cancel(); Assert.False(rename.IsCurrent);
                rename.Release(); Assert.Equal(1, releases);
                rename.Release(); Assert.Equal(1, releases);
                Assert.Throws<ObjectDisposedException>(() => rename.Retain());
            }
        }

        [Fact]
        public void RetainedRenameStillObservesActualSourceContextChanges()
        {
            bool current = true;
            var lease = new ReferenceIntentLease(() => current, CancellationToken.None, null);
            var rename = lease.Retain(); lease.Release();
            current = false; Assert.False(rename.IsCurrent);
            rename.Release(); Assert.False(rename.IsCurrent);
        }
    }
}
