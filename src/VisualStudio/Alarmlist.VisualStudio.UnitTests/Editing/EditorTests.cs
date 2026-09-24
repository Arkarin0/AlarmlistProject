// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private readonly AlmxEditingService _editing = new AlmxEditingService();
        private const string Source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>Old</Name><Code>1</Code></Alarm><Alarm><FullyQualifiedName>B</FullyQualifiedName></Alarm></Alarmlist>";

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void ScalarEditPreservesEveryOtherCharacter(string newline)
        {
            string source = "<?xml version='1.0'?>" + newline + "<!-- <Alarm/> -->" + newline
                + "<Alarmlist extra='>'><Alarm attr='keep'><Name flag='x'>old &amp; value</Name>"
                + "<Procedures><Clear/><Unknown>keep</Unknown></Procedures><Extra><![CDATA[<keep/>]]></Extra></Alarm></Alarmlist>";
            AlmxProjection parsed = _editing.Parse(source);
            SourceEdit edit = _editing.SetField(parsed, parsed.Alarms[0], "Name", "new <&> 😀\r\nline");
            string result = edit.Apply(source);
            Assert.Equal(source.Replace("old &amp; value", "new &lt;&amp;&gt; 😀&#xD;\nline"), result);
            Assert.Equal("new <&> 😀\r\nline", _editing.Parse(result).Alarms[0].GetValue("Name"));
        }

        [Theory]
        [InlineData("<Name/>")]
        [InlineData("<Name data='a' />")]
        [InlineData("<Name><![CDATA[<old>]]></Name>")]
        public void EmptyAndCdataScalarsAreEditable(string field)
        {
            string source = "<Alarmlist><Alarm>" + field + "</Alarm></Alarmlist>";
            AlmxProjection parsed = _editing.Parse(source);
            string result = _editing.SetField(parsed, parsed.Alarms[0], "Name", "<&").Apply(source);
            Assert.Equal("<&", _editing.Parse(result).Alarms[0].GetValue("Name"));
            if (field.Contains("data=")) Assert.Contains("data='a'", result);
        }

        [Theory]
        [InlineData("<Alarmlist/>")]
        [InlineData("<Alarmlist />")]
        [InlineData("<Alarmlist xmlns='urn:alarm' />")]
        [InlineData("<a:Alarmlist xmlns:a='urn:alarm'/>")]
        [InlineData("<Alarmlist>\r\n  <!-- keep -->\r\n</Alarmlist>")]
        public void AddSupportsEmptyRootsAndNamespaces(string source)
        {
            string result = _editing.AddAlarm(_editing.Parse(source)).Apply(source);
            AlmxProjection parsed = _editing.Parse(result);
            Assert.True(parsed.IsValid, parsed.Error);
            Assert.Equal("NewAlarm", Assert.Single(parsed.Alarms).Identity);
            var document = System.Xml.Linq.XDocument.Parse(result);
            Assert.All(document.Root.Descendants(), child => Assert.Equal(document.Root.Name.Namespace, child.Name.Namespace));
            result = _editing.AddAlarm(parsed).Apply(result);
            Assert.Equal(new[] { "NewAlarm", "NewAlarm1" }, _editing.Parse(result).Alarms.Select(alarm => alarm.Identity));
            if (source.Contains("<!-- keep -->")) Assert.Contains("<!-- keep -->", result);
        }

        [Fact]
        public void AbsentAndEmptyFieldsStayDistinct()
        {
            AlmxProjection parsed = _editing.Parse("<Alarmlist><Alarm/></Alarmlist>");
            Assert.False(parsed.Alarms[0].HasField("Name"));
            string text = _editing.SetField(parsed, parsed.Alarms[0], "Name", "").Apply(parsed.Text);
            parsed = _editing.Parse(text);
            Assert.True(parsed.Alarms[0].HasField("Name"));
            Assert.Equal("", parsed.Alarms[0].GetValue("Name"));
            text = _editing.SetField(parsed, parsed.Alarms[0], "Name", null).Apply(text);
            Assert.False(_editing.Parse(text).Alarms[0].HasField("Name"));
        }

        [Theory]
        [InlineData("<Name>a</Name><Name>b</Name>")]
        [InlineData("<Name>a<!-- keep -->b</Name>")]
        [InlineData("<Name><Unknown/></Name>")]
        public void UnsafeScalarEditsAreRejected(string fields)
        {
            AlmxProjection parsed = _editing.Parse("<Alarmlist><Alarm>" + fields + "</Alarm></Alarmlist>");
            Assert.Throws<InvalidOperationException>(() => _editing.SetField(parsed, parsed.Alarms[0], "Name", "overwrite"));
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm>")]
        [InlineData("<AlarmList/>")]
        [InlineData("<!DOCTYPE Alarmlist [<!ENTITY a 'text'>]><Alarmlist/>")]
        [InlineData("")]
        public void InvalidDocumentsRejectDesignerChanges(string text)
        {
            AlmxProjection parsed = _editing.Parse(text);
            Assert.False(parsed.IsValid);
            Assert.NotEmpty(parsed.Error);
            Assert.Throws<InvalidOperationException>(() => _editing.AddAlarm(parsed));
        }

        [Fact]
        public void DeletePreservesAdjacentCommentsAndUnknownElements()
        {
            string text = "<Alarmlist><!-- before --><Alarm><Name>delete</Name><Procedures><Clear/></Procedures></Alarm><!-- after --><Extra/></Alarmlist>";
            AlmxProjection parsed = _editing.Parse(text);
            Assert.Equal("<Alarmlist><!-- before --><!-- after --><Extra/></Alarmlist>", _editing.DeleteAlarm(parsed, parsed.Alarms[0]).Apply(text));
        }

        [Fact]
        public void DifferentFieldsRebaseButSameFieldConflicts()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            {
                AlmxProjection baseline = model.Projection;
                AlarmSource alarm = baseline.Alarms[0];
                Assert.True(model.SetField(baseline, alarm, "Name", "First view", out _));
                Assert.True(model.SetField(baseline, alarm, "Code", "2", out _));
                Assert.False(model.SetField(baseline, alarm, "Name", "Lost edit", out string error));
                Assert.Contains("another view", error);
                Assert.Equal("First view", model.Projection.Alarms[0].GetValue("Name"));
                Assert.Equal("2", model.Projection.Alarms[0].GetValue("Code"));
            }
        }

        [Fact]
        public void MultiFieldCommitIsAtomicWhenOneFieldConflicts()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            {
                AlmxProjection baseline = model.Projection;
                Assert.True(model.SetField(baseline, baseline.Alarms[0], "Code", "other", out _));
                Assert.False(model.SetFields(baseline, baseline.Alarms[0], new Dictionary<string, string> { ["Name"] = "draft", ["Code"] = "draft" }, out _));
                Assert.Equal("Old", model.Projection.Alarms[0].GetValue("Name"));
                Assert.Equal(1, buffer.Edits);
            }
        }

        [Fact]
        public void UnnamedOrDuplicateAlarmCannotRebase()
        {
            var buffer = new TestBuffer(Source.Replace("<FullyQualifiedName>A</FullyQualifiedName>", ""));
            using (var model = new AlmxDocumentModel(buffer, _editing))
            {
                AlmxProjection baseline = model.Projection;
                buffer.SetText(buffer.Current.Text + " ");
                Assert.False(model.SetField(baseline, baseline.Alarms[0], "Name", "unsafe", out _));
            }
        }

        [Fact]
        public void TwoViewsShareEditsButKeepTheirSelectionAndDrafts()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var first = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            using (var second = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                second.Selected = second.Alarms[1];
                first.Fields.Single(field => field.Name == "Name").Value = "Updated";
                Assert.True(first.HasPending);
                Assert.Equal("Old", model.Projection.Alarms[0].GetValue("Name"));
                Assert.True(first.CommitPending());
                Assert.Equal("Updated", second.Alarms[0].Title);
                Assert.Equal("B", second.Selected.Identity);
                second.Filter = "B";
                Assert.Single(second.Alarms);
                Assert.Equal(2, first.Alarms.Count);
                Assert.Equal(1, buffer.Edits);
            }
        }

        [Fact]
        public void PendingConflictCanBeCancelledAndMalformedXmlRecovers()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "Name").Value = "draft";
                buffer.SetText(Source.Replace("Old", "external"));
                Assert.False(view.CommitPending());
                Assert.True(view.HasPending);
                view.CancelPending();
                Assert.Equal("external", view.Fields.Single(field => field.Name == "Name").Value);
                buffer.SetText("<Alarmlist>");
                Assert.False(view.IsValid);
                Assert.False(view.AddCommand.CanExecute(null));
                buffer.SetText(Source);
                Assert.True(view.IsValid);
                Assert.Equal(2, view.Alarms.Count);
            }
        }

        [Fact]
        public void SeveralDraftFieldsIncludingIdentityCommitInOneEdit()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "FullyQualifiedName").Value = "Renamed";
                view.Fields.Single(field => field.Name == "Name").Value = "New";
                Assert.True(view.CommitPending());
                Assert.Equal("Renamed", model.Projection.Alarms[0].Identity);
                Assert.Equal("New", model.Projection.Alarms[0].GetValue("Name"));
                Assert.Equal(1, buffer.Edits);
            }
        }

        [Fact]
        public void RejectedBufferEditPreservesDraftAndModelDisposalUnsubscribes()
        {
            var buffer = new TestBuffer(Source) { RejectEdit = true };
            var model = new AlmxDocumentModel(buffer, _editing);
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "Name").Value = "draft";
                Assert.False(view.CommitPending());
                Assert.True(view.HasPending);
                Assert.Equal(Source, buffer.Current.Text);
            }
            model.Dispose();
            Assert.Equal(0, buffer.Subscribers);
        }

        [Fact]
        public void SelectionAfterCommittingDraftUsesTheCurrentProjection()
        {
            var buffer = new TestBuffer(Source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "Name").Value = "first";
                view.Selected = view.Alarms[1];
                AlarmFieldViewModel name = view.Fields.Single(field => field.Name == "Name");
                name.IsPresent = true;
                name.Value = "second";
                Assert.True(view.CommitPending());
                Assert.Equal(new[] { "first", "second" }, model.Projection.Alarms.Select(alarm => alarm.GetValue("Name")));
            }
        }

        [Fact]
        public void AmbiguousIdentityAndStaleDeleteNeverModifyTheBuffer()
        {
            var buffer = new TestBuffer(Source.Replace(">B<", ">A<"));
            using (var model = new AlmxDocumentModel(buffer, _editing))
            {
                AlmxProjection baseline = model.Projection;
                buffer.SetText(buffer.Current.Text + " ");
                Assert.False(model.SetField(baseline, baseline.Alarms[0], "Name", "unsafe", out _));
                Assert.False(model.DeleteAlarm(baseline.Alarms[0], out _));
                Assert.Equal(0, buffer.Edits);
            }
        }

        [Fact]
        public void ConflictInDesignerModeStillAllowsShowingXmlForRepair()
        {
            var settings = new TestSettings();
            settings.Write("Mode", "Designer");
            var layout = new EditorLayoutViewModel(settings, () => false, false);
            layout.SetMode(EditorMode.XML);
            Assert.Equal(EditorMode.Split, layout.Mode);
        }

        [Fact]
        public void LayoutChangesPersistWithoutChangingDocumentAndCommitCanBlockSwitch()
        {
            var settings = new TestSettings();
            bool canCommit = false;
            var layout = new EditorLayoutViewModel(settings, () => canCommit, false);
            layout.SetMode(EditorMode.XML);
            Assert.Equal(EditorMode.Split, layout.Mode);
            canCommit = true;
            layout.SetMode(EditorMode.XML);
            layout.SideBySide = true;
            layout.Swapped = true;
            layout.Proportion = 0.65;
            var restored = new EditorLayoutViewModel(settings, () => true, false);
            Assert.Equal(EditorMode.XML, restored.Mode);
            Assert.True(restored.SideBySide);
            Assert.True(restored.Swapped);
            Assert.Equal(0.65, restored.Proportion);
        }

        private sealed class TestSettings : IEditorLayoutSettings
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Read(string name, string fallback) => _values.TryGetValue(name, out string value) ? value : fallback;
            public void Write(string name, string value) => _values[name] = value;
        }
        private sealed class TestBuffer : IDocumentBuffer
        {
            private EventHandler _changed;
            public event EventHandler Changed { add => _changed += value; remove => _changed -= value; }
            public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
            public int Edits { get; private set; }
            public bool RejectEdit { get; set; }
            public DocumentSnapshot Current { get; private set; }
            public TestBuffer(string text) { Current = new DocumentSnapshot(text, 0); }
            public void SetText(string text) { Current = new DocumentSnapshot(text, Current.Version + 1); _changed?.Invoke(this, EventArgs.Empty); }
            public bool TryApply(int version, SourceEdit edit, string description, out string error)
            {
                if (RejectEdit || version != Current.Version) { error = "Rejected"; return false; }
                Edits++;
                SetText(edit.Apply(Current.Text));
                error = null;
                return true;
            }
        }
    }
}
