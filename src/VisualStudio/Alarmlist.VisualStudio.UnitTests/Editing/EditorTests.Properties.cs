// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

// The existing guarded view commands intentionally expose their completion tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string PropertyChild = "<Alarmlist><Properties><Property Name='par1' Value='001'/></Properties><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>";
        private const string PropertyBase = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Properties><Property Name='par1' Value='ignored'/></Properties><Code>$(par1)</Code><Description>Inspect $(AlarmlistAlarmCode)</Description><TestProcedure><Group Name='Instructions'><Step>Check $(par1) $$(escaped)</Step></Group></TestProcedure></Alarm></Alarmlist>";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<&\"'\r\n\t$(literal)")]
        public void ParameterAttributeEditsPreserveXmlAndAssignmentPresence(string value)
        {
            const string source = "<Alarmlist extra='keep'><Alarm><Properties><!--keep--><Property Name = 'par1' Value = 'old' ></Property><Unknown/></Properties><Other/></Alarm></Alarmlist>";
            AlmxProjection parsed = _editing.Parse(source);
            string result = _editing.SetProperty(parsed, parsed.Alarms[0], new PropertyValueChange("par1", "renamed", value)).Apply(source);
            PropertySource property = Assert.Single(_editing.Parse(result).Alarms[0].Properties.Properties);
            Assert.Equal("renamed", property.Name);
            Assert.Equal(value, property.Value);
            Assert.Contains("<!--keep--><Property Name = 'renamed'", result);
            Assert.Contains(" ></Property><Unknown/></Properties><Other/>", result);
            if (value != null) Assert.Contains("Value = '", result);
            else Assert.DoesNotContain("Value", result);
        }

        [Theory]
        [InlineData("<Alarmlist/>")]
        [InlineData("<a:Alarmlist xmlns:a='urn:test'><a:Properties/></a:Alarmlist>")]
        public void FileParametersCanBeAddedWithoutAnAlarm(string source)
        {
            var buffer = new TestBuffer(source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.FileParameters.AddCommand.Execute(null);
                Assert.Null(view.Error);
                Assert.Null(Assert.Single(model.Projection.Properties.Properties).Value);
                var xml = System.Xml.Linq.XDocument.Parse(buffer.Current.Text);
                Assert.All(xml.Root.Descendants(), element => Assert.Equal(xml.Root.Name.Namespace, element.Name.Namespace));
                ParameterViewModel row = Assert.Single(view.FileParameters.Rows);
                row.HasValue = true;
                Assert.True(view.CommitPending(), view.Error);
                Assert.Equal("", Assert.Single(model.Projection.Properties.Properties).Value);
                buffer.Undo();
                Assert.Null(Assert.Single(model.Projection.Properties.Properties).Value);
                buffer.Undo();
                Assert.Equal(source, buffer.Current.Text);
            }
        }

        [Theory]
        [InlineData("<Property Name='p' extra='keep'/>")]
        [InlineData("<Property Name='p'><!--keep--></Property>")]
        [InlineData("<Property Name='p'>text</Property>")]
        [InlineData("<Property Name='p'/><Property Name='P'/>")]
        public void UnsupportedOrAmbiguousParametersAreNotOverwritten(string content)
        {
            AlmxProjection parsed = _editing.Parse("<Alarmlist><Properties>" + content + "</Properties></Alarmlist>");
            Assert.Throws<InvalidOperationException>(() => _editing.SetProperty(parsed, null, new PropertyValueChange("p", "p", "new")));
        }

        [Fact]
        public void ParameterAndFieldDraftsCommitTogetherAndRejectConcurrentParameterChanges()
        {
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p' Value='old'/></Properties><Code>old</Code></Alarm></Alarmlist>";
            var buffer = new TestBuffer(source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Fields.Single(field => field.Name == "Code").Value = "$(p)";
                Assert.Single(view.AlarmParameters.Rows).Value = "001";
                Assert.True(view.CommitPending(), view.Error);
                Assert.Equal(1, buffer.Edits);
                Assert.Contains("<Code>$(p)</Code>", buffer.Current.Text);
                Assert.Contains("Value='001'", buffer.Current.Text);
                buffer.Undo();
                Assert.Equal(source, buffer.Current.Text);
                Assert.Single(view.AlarmParameters.Rows).Value = "draft";
                buffer.SetText(source.Replace("Value='old'", "Value='external'"));
                Assert.Equal("draft", Assert.Single(view.AlarmParameters.Rows).Value);
                Assert.False(view.CommitPending());
                Assert.Contains("another view", view.Error);
                view.CancelPending();
                Assert.Equal("external", Assert.Single(view.AlarmParameters.Rows).Value);
            }
        }

        [Fact]
        public void AbstractTogglePreservesOtherAttributesAndHasOneUndo()
        {
            const string source = "<Alarmlist><Alarm other = 'keep' abstract = 'false'><FullyQualifiedName>A</FullyQualifiedName></Alarm></Alarmlist>";
            var buffer = new TestBuffer(source);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.IsAbstract = true;
                Assert.True(view.IsAbstract);
                Assert.Equal(source.Replace("'false'", "'true'"), buffer.Current.Text);
                Assert.Equal(1, buffer.Edits);
                buffer.Undo();
                Assert.False(view.IsAbstract);
                Assert.Equal(source, buffer.Current.Text);
            }
        }

        [Fact]
        public void ParameterTableSeparatesTemplateDeclarationsFromConsumerValues()
        {
            using (var session = new SemanticSession(PropertyChild))
            {
                session.Analyze(PropertyBase);
                ParameterViewModel row = Assert.Single(session.View.AlarmParameters.Rows);
                Assert.False(row.IsLocal);
                Assert.Equal("001", row.EffectiveDisplay);
                Assert.Contains("Declaration: Alarm Base", row.OriginText);
                Assert.Contains("Assignment: File", row.OriginText);
                row.DeclareCommand.Execute(null);
                session.Analyze(PropertyBase);
                row = Assert.Single(session.View.AlarmParameters.Rows);
                Assert.True(row.IsLocal);
                Assert.False(row.HasValue);
                Assert.Equal("001", row.EffectiveDisplay);
                row.HasValue = true;
                Assert.True(session.View.CommitPending(), session.View.Error);
                session.Analyze(PropertyBase);
                Assert.Equal("(empty)", Assert.Single(session.View.AlarmParameters.Rows).EffectiveDisplay);
                Assert.Equal("", session.Field("Code").EffectiveValue);
                row = Assert.Single(session.View.AlarmParameters.Rows);
                row.HasValue = false;
                Assert.True(session.View.CommitPending(), session.View.Error);
                session.Analyze(PropertyBase);
                Assert.Equal("001", row.EffectiveDisplay);
                row.DeleteCommand.Execute(null);
                session.Analyze(PropertyBase);
                Assert.False(Assert.Single(session.View.AlarmParameters.Rows).IsLocal);
                Assert.DoesNotContain("ignored", session.Buffer.Current.Text);
            }
        }

        [Fact]
        public async Task CopiesKeepRawExpressionsAndFollowLaterFileParameterChangesAsync()
        {
            using (var session = new SemanticSession(PropertyChild))
            {
                session.Analyze(PropertyBase);
                Assert.Equal("001", session.Field("Code").EffectiveValue);
                session.Field("Code").OverrideCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal("$(par1)", session.Field("Code").Value);
                Assert.True(session.View.CommitPending(), session.View.Error);
                session.Analyze(PropertyBase);
                session.View.ProcedureSections[0].MakeLocalCopyCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Contains("<Clear", session.Buffer.Current.Text);
                Assert.Contains("Check $(par1) $$(escaped)", session.Buffer.Current.Text);
                Assert.Single(session.View.FileParameters.Rows).Value = "002";
                Assert.True(session.View.CommitPending(), session.View.Error);
                session.Analyze(PropertyBase);
                Assert.Equal("002", session.Field("Code").ResolvedValue);
                Assert.Equal("Inspect 002", session.Field("Description").EffectiveValue);
                Assert.Contains("Check 002 $(escaped)", session.View.CompiledXml);
                Assert.Contains("<Code>$(par1)</Code>", session.Buffer.Current.Text);
            }
        }

        [Fact]
        public void AbstractPreviewDoesNotPretendToHaveEvaluatedValues()
        {
            using (var session = new SemanticSession(PropertyChild.Replace("<Alarm>", "<Alarm abstract='true'>")))
            {
                session.Analyze(PropertyBase);
                Assert.True(session.View.IsAbstract);
                Assert.Empty(session.View.ResolvedFields);
                Assert.Contains("template", session.View.ResolvedPreviewMessage);
                Assert.DoesNotContain("<Alarm>", session.View.CompiledXml);
                Assert.Equal("$(par1)", session.Field("Code").EffectiveValue);
            }
        }
    }
}
