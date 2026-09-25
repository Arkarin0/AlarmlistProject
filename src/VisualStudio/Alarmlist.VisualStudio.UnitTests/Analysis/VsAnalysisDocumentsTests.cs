// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public class VsAnalysisDocumentsTests
    {
        [Fact]
        public void ObservingDocumentsDoesNotKeepVsOwnedDocumentsOrBuffersAlive()
        {
            var factory = new InterfaceStub<ITextDocumentFactoryService>();
            using (var observer = new VsAnalysisDocuments(null, null, factory.Object))
            {
                WeakReference[] retired = ObserveAndRelease(factory);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Assert.All(retired, reference => Assert.False(reference.IsAlive));
                GC.KeepAlive(observer);
                GC.KeepAlive(factory);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] ObserveAndRelease(InterfaceStub<ITextDocumentFactoryService> factory)
        {
            var buffer = new InterfaceStub<ITextBuffer>();
            InterfaceStub<ITextDocument> document = Document(buffer);
            factory.Raise("TextDocumentCreated", new TextDocumentEventArgs(document.Object));
            Assert.Equal(1, buffer.Subscribers("Changed"));
            return new[] { new WeakReference(document.Object), new WeakReference(buffer.Object) };
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ObservationsTrackRenamesAndDetachWithoutDisposingVsBuffers(bool documentDisposed)
        {
            var factory = new InterfaceStub<ITextDocumentFactoryService>();
            var buffer = new InterfaceStub<ITextBuffer>();
            InterfaceStub<ITextDocument> document = Document(buffer);
            using (var observer = new VsAnalysisDocuments(null, null, factory.Object))
            {
                var changes = new List<string>();
                observer.Changed += (sender, args) => changes.Add(args.Path);
                factory.Raise("TextDocumentCreated", new TextDocumentEventArgs(document.Object));
                factory.Raise("TextDocumentCreated", new TextDocumentEventArgs(document.Object));
                Assert.Equal(1, buffer.Subscribers("Changed"));
                Assert.Equal(1, document.Subscribers("FileActionOccurred"));
                changes.Clear();
                buffer.Raise("Changed", null);
                document.Values["FilePath"] = "renamed.almx";
                document.Raise("FileActionOccurred", new TextDocumentFileActionEventArgs("renamed.almx", DateTime.UtcNow, FileActionTypes.DocumentRenamed));
                Assert.Equal(new[] { "source.almx", "source.almx", "renamed.almx" }, changes);
                if (documentDisposed)
                    factory.Raise("TextDocumentDisposed", new TextDocumentEventArgs(document.Object));
                else observer.Dispose();
                changes.Clear();
                Assert.Equal(0, buffer.Subscribers("Changed"));
                Assert.Equal(0, document.Subscribers("FileActionOccurred"));
                buffer.Raise("Changed", null);
                Assert.Empty(changes);
            }
            Assert.Equal(0, factory.Subscribers("TextDocumentCreated"));
            Assert.Equal(0, factory.Subscribers("TextDocumentDisposed"));
        }

        private static InterfaceStub<ITextDocument> Document(InterfaceStub<ITextBuffer> buffer)
        {
            var document = new InterfaceStub<ITextDocument>();
            document.Values["FilePath"] = "source.almx";
            document.Values["TextBuffer"] = buffer.Object;
            return document;
        }

        // Strict interface/event stand-ins avoid loading an actual VS editor.
        // Any ownership operation (including Dispose) fails as an unexpected call.
        private sealed class InterfaceStub<T> : RealProxy where T : class
        {
            private readonly Dictionary<string, Delegate> _events = new Dictionary<string, Delegate>();
            public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
            public T Object => (T)GetTransparentProxy();
            public InterfaceStub() : base(typeof(T)) { }
            public int Subscribers(string name) => _events.TryGetValue(name, out Delegate handlers) ? handlers?.GetInvocationList().Length ?? 0 : 0;
            public void Raise(string name, EventArgs args)
            {
                if (_events.TryGetValue(name, out Delegate handlers)) handlers?.DynamicInvoke(Object, args);
            }
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                string name = call.MethodName;
                object result = null;
                if (name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal))
                {
                    string eventName = name.Substring(name.IndexOf('_') + 1);
                    _events.TryGetValue(eventName, out Delegate handlers);
                    _events[eventName] = name.StartsWith("add_", StringComparison.Ordinal)
                        ? Delegate.Combine(handlers, (Delegate)call.Args[0]) : Delegate.Remove(handlers, (Delegate)call.Args[0]);
                }
                else if (name.StartsWith("get_", StringComparison.Ordinal) && Values.TryGetValue(name.Substring(4), out object value)) result = value;
                else if (name == nameof(GetHashCode)) result = RuntimeHelpers.GetHashCode(this);
                else if (name == nameof(Equals)) result = ReferenceEquals(Object, call.Args[0]);
                else if (name == nameof(ToString)) result = typeof(T).Name;
                else return new ReturnMessage(new InvalidOperationException("Unexpected editor call: " + name), call);
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
        }
    }
}
