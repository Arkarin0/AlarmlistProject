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

            Assert.Equal(expectedType, typeof(TestProcedureSyntax).GetProperty(nameof(TestProcedureSyntax.Instructions)).PropertyType);
            Assert.Equal(expectedType, typeof(TestProcedureSyntax).GetProperty(nameof(TestProcedureSyntax.Reset)).PropertyType);
        }

        [Fact]
        public void TestProcedureStepsAndClearCanBeUsedInTestProcedureSections()
        {
            var testProcedure = new TestProcedureSyntax();
            var step = new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Do something.");
            var clear = new Clear();

            testProcedure.Instructions.Add(step);
            testProcedure.Instructions.Add(clear);

            Assert.Equal(new ITestProcedureItem[] { step, clear }, testProcedure.Instructions);
        }
    }
}
