using System.Linq;
using Alarmlist.Syntax;
using Xunit;

namespace Alarmlist.Syntax.Tests
{
    public class TestProcedureSyntaxTests
    {
        [Fact]
        public void InstructionsAndResetOnlyAcceptTestProcedureItems()
        {
            var expectedType = typeof(ReferenceableCollection<ITestProcedureItem>);

            Assert.Equal(expectedType, typeof(ProcedureGroupSyntax).GetProperty(nameof(ProcedureGroupSyntax.Entries)).PropertyType);
        }

        [Fact]
        public void TestProcedureStepsAndClearCanBeUsedInTestProcedureSections()
        {
            var testProcedure = new TestProcedureSyntax();
            var step = new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Do something.");
            var clear = new Clear();

            testProcedure.GetOrAddGroup("Instructions").Entries.Add(step);
            testProcedure.GetOrAddGroup("Instructions").Entries.Add(clear);

            Assert.Equal(new ITestProcedureItem[] { step, clear }, testProcedure.GetOrAddGroup("Instructions").Entries);
        }
    }
}
