// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public sealed class ReferenceEditorTargetTests
    {
        [Fact]
        public void NativeArgumentRoutingAcceptsOnlyEmptyPrimitiveVariants()
        {
            Assert.True(XmlReferenceCommands.HasNoArguments(IntPtr.Zero));
            IntPtr variant = Marshal.AllocHGlobal(24);
            IntPtr empty = Marshal.StringToBSTR("");
            IntPtr text = Marshal.StringToBSTR("argument");
            try
            {
                Marshal.WriteInt16(variant, 0); Assert.True(XmlReferenceCommands.HasNoArguments(variant));
                Marshal.WriteInt16(variant, 1); Assert.True(XmlReferenceCommands.HasNoArguments(variant));
                Marshal.WriteInt16(variant, 8); Marshal.WriteIntPtr(variant, 8, empty);
                Assert.True(XmlReferenceCommands.HasNoArguments(variant));
                Marshal.WriteIntPtr(variant, 8, text); Assert.False(XmlReferenceCommands.HasNoArguments(variant));
                Marshal.WriteInt16(variant, 0x4008); Assert.False(XmlReferenceCommands.HasNoArguments(variant));
            }
            finally { Marshal.FreeBSTR(text); Marshal.FreeBSTR(empty); Marshal.FreeHGlobal(variant); }
        }
        [Fact]
        public void EditorTargetsAdoptSavedAuxiliaryVersionsOnlyAfterFullTextComparison()
        {
            const string resource = "<root><data name='Key'><value>text</value></data></root>";
            var request = new AnalysisReferenceRequest(new[] { new AnalysisInput("<Alarmlist/>", "Source.almx", 8) },
                new AnalysisContext(resources: new[] { new AnalysisResourceInput(resource, "Labels.resx", 0) }),
                new[] { new AnalysisInput("<Project/>", "App.almproj", 0) });
            var token = ReferenceEditorTarget.Resolve(request, new AnalysisInput(resource, "labels.resx", 42), 20);
            Assert.Equal("Labels.resx", token.Location.FilePath); Assert.Equal(0, token.Location.SourceVersion); Assert.Equal(20, token.Offset);
            Assert.Equal(0, ReferenceEditorTarget.Resolve(request, new AnalysisInput("<Project/>", "App.almproj", 19), 1).Location.SourceVersion);
            Assert.Throws<InvalidOperationException>(() => ReferenceEditorTarget.Resolve(request, new AnalysisInput(resource + "<!--unsaved-->", "Labels.resx", 43), 20));
            Assert.Throws<InvalidOperationException>(() => ReferenceEditorTarget.Resolve(request, new AnalysisInput("<Alarmlist/>", "Source.almx", 9), 1));
            Assert.Equal(8, ReferenceEditorTarget.Resolve(request, new AnalysisInput("<Alarmlist/>", "Source.almx", 8), 1).Location.SourceVersion);
            Assert.Null(ReferenceEditorTarget.Resolve(request, new AnalysisInput("<ordinary/>", "Other.xml", 0), 1));
        }

        [Fact]
        public void ReferenceDocumentOwnershipUsesCapturedMembershipAndPreservesOtherNativeXml()
        {
            var project = new AnalysisProject("App.almproj", "Debug", null, new[] { "Shared.almx" },
                resources: new[] { new AnalysisResourceItem("Labels.resx") }, lookups: new[] { "Hmi.almlookup" });
            Assert.True(ReferenceDocumentOwnership.Contains(project, "app.almproj"));
            Assert.True(ReferenceDocumentOwnership.Contains(project, "shared.almx"));
            Assert.True(ReferenceDocumentOwnership.Contains(project, "labels.resx"));
            Assert.True(ReferenceDocumentOwnership.Contains(project, "hmi.almlookup"));
            Assert.False(ReferenceDocumentOwnership.Contains(project, "Other.xml"));
            Assert.False(XmlReferenceCommands.SupportsExtension(".cs"));
            Assert.True(XmlReferenceCommands.SupportsExtension(".ALMPROJ"));
            Assert.True(XmlReferenceCommands.SupportsExtension(".projitems"));
            Assert.True(XmlReferenceCommands.Command(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.FindReferences, out bool rename)); Assert.False(rename);
            Assert.True(XmlReferenceCommands.Command(VSConstants.VSStd2K, (uint)VSConstants.VSStd2KCmdID.RENAME, out rename)); Assert.True(rename);
            Assert.False(XmlReferenceCommands.Command(VSConstants.GUID_VSStandardCommandSet97, (uint)VSConstants.VSStd97CmdID.Rename, out _));
        }
    }
}
