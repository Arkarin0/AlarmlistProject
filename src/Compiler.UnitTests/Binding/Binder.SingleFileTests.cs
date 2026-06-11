using Xunit;
using Alarmlist.Syntax;
// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Text;
using Moq;
using Alarmlist.Compiler.Test;
using Alarmlist.Diagnostics;

namespace Alarmlist.Core.UnitTests.Binding
{
    public class BinderSingleFileTests
    {
        public static Fakes.BinderMock CreateInstance()
        {
            return BinderTests.CreateInstance();
        }

        public static SourceText CreateSourceText()
        {
            var mock = new Mock<SourceText>();
            return mock.Object;
        }

        public static SourceText CreateSourceText(AlarmSyntaxTree alarmSyntaxTree)
        {
            var mock = new Fakes.SourceTextMock(alarmSyntaxTree);
                        
            return mock;
        }



        [Fact()]
        public void SimpleAlarmsAreAddedToTheSyntaxTreeTest()
        {
            var obj = CreateInstance();
            var expected = TestHelper.CreateAlarmSyntaxTreeByItemCounts(1);
            var src = CreateSourceText(expected);

            obj.AddSourceText(src);
            var result = obj.Bind();
            var actual = result.SyntaxTree;


            Assert.True(result.Success);
            TestHelper.AssertContains(actual, expected);
        }

        [Fact()]
        public void BindResolvesSingleReferenceChainTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var baseAlarm = TestHelper.CreateAlarmSyntaxNode("A");
            var middleAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameB");
            middleAlarm.ReferenceName = baseAlarm.FullyQualifiedName;
            var childAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameC");
            childAlarm.ReferenceName = middleAlarm.FullyQualifiedName;

            tree.Alarms.Add(baseAlarm);
            tree.Alarms.Add(middleAlarm);
            tree.Alarms.Add(childAlarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();
            var actual = result.SyntaxTree;

            Assert.True(result.Success);
            Assert.Same(baseAlarm, middleAlarm.Reference);
            Assert.Same(middleAlarm, childAlarm.Reference);
            Assert.Equal(baseAlarm.Name, childAlarm.Name);
            Assert.Equal(baseAlarm.Code, childAlarm.Code);
            Assert.Equal(baseAlarm.Category, childAlarm.Category);
            Assert.Equal(baseAlarm.Description, childAlarm.Description);
            Assert.Contains(childAlarm, actual.Alarms);
        }

        [Fact()]
        public void BindKeepsLocalValuesWhenReferenceProvidesDefaultsTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var baseAlarm = TestHelper.CreateAlarmSyntaxNode("A");
            var childAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameB");
            childAlarm.ReferenceName = baseAlarm.FullyQualifiedName;
            childAlarm.Name = "Local alarm name";

            tree.Alarms.Add(baseAlarm);
            tree.Alarms.Add(childAlarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();

            Assert.True(result.Success);
            Assert.Equal("Local alarm name", childAlarm.Name);
            Assert.Equal(baseAlarm.Code, childAlarm.Code);
        }

        [Fact()]
        public void BindReportsDiagnosticWhenReferenceIsMissingTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var childAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameB");
            childAlarm.ReferenceName = "AlarmNamespace.DoesNotExist";

            tree.Alarms.Add(childAlarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == DiagnosticWellKnownIds.MissingReference);
            Assert.Null(childAlarm.Reference);
        }

        [Fact()]
        public void BindReportsDiagnosticWhenAlarmNameIsDuplicatedTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var firstAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameA");
            var secondAlarm = Alarmlist.Core.AlarmlistFactory.Alarm(firstAlarm.FullyQualifiedName);

            tree.Alarms.Add(firstAlarm);
            tree.Alarms.Add(secondAlarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == DiagnosticWellKnownIds.DuplicateAlarmName);
        }

        [Fact()]
        public void BindReportsDiagnosticWhenAlarmReferencesItselfTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var alarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameA");
            alarm.ReferenceName = alarm.FullyQualifiedName;

            tree.Alarms.Add(alarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == DiagnosticWellKnownIds.SelfReference);
            Assert.Null(alarm.Reference);
        }

        [Fact()]
        public void BindReportsDiagnosticWhenReferenceChainContainsCycleTest()
        {
            var obj = CreateInstance();
            var tree = new AlarmSyntaxTree();
            var firstAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameA");
            var secondAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameB");
            var thirdAlarm = Alarmlist.Core.AlarmlistFactory.Alarm("AlarmNamespace.AlarmNameC");
            firstAlarm.ReferenceName = thirdAlarm.FullyQualifiedName;
            secondAlarm.ReferenceName = firstAlarm.FullyQualifiedName;
            thirdAlarm.ReferenceName = secondAlarm.FullyQualifiedName;

            tree.Alarms.Add(firstAlarm);
            tree.Alarms.Add(secondAlarm);
            tree.Alarms.Add(thirdAlarm);
            obj.AddSourceText(CreateSourceText(tree));

            var result = obj.Bind();

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == DiagnosticWellKnownIds.CircularReference);
            Assert.Null(firstAlarm.Reference);
            Assert.Null(secondAlarm.Reference);
            Assert.Null(thirdAlarm.Reference);
        }

    }
}
