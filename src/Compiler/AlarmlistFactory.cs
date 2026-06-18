using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;

namespace Alarmlist.Core
{
    public partial class AlarmlistFactory
    {
        //public static Compiler.AlarmList ReadFromXMLFiles(IEnumerable<string> filepaths)
        //{
        //    //var abc= new Compiler.XML.XMLAlarmlistSerilaizer().DeserializeMultiple(filepaths);
        //    //return abc.ToErrorDataList();
        //    throw new NotImplementedException();
        //}

        public static Compiler.Alarm CreateAlarmFromSyntaxNode(Alarmlist.Syntax.AlarmSyntaxNode node)
        {
            var alarm = new Compiler.Alarm(
                fullyQualifiedName: node.FullyQualifiedName,
                name: node.Name,
                code: node.Code,
                description: node.Description,
                category: node.Category,
                testProcedure: CreateTestProcedureFromSyntaxNode(node.ResolvedTestProcedures));


            return alarm;
        }

        private static Compiler.TestProcedure CreateTestProcedureFromSyntaxNode(TestProcedureSyntax testProcedure)
        {
            return new Compiler.TestProcedure(
                instructions: testProcedure?.Instructions.Select(CreateTestProcedureItemFromSyntaxNode),
                reset: testProcedure?.Reset.Select(CreateTestProcedureItemFromSyntaxNode));
        }

        private static Compiler.TestProcedureItem CreateTestProcedureItemFromSyntaxNode(ITestProcedureItem item)
        {
            if (item is TestProcedureStepSyntax step)
                return new Compiler.TestProcedureItem(CreateTestProcedureItemKind(step.Kind), step.Text);

            return new Compiler.TestProcedureItem(Compiler.TestProcedureItemKind.Instruction);
        }

        private static Compiler.TestProcedureItemKind CreateTestProcedureItemKind(TestProcedureStepKind kind)
        {
            switch (kind)
            {
                case TestProcedureStepKind.Hint:
                    return Compiler.TestProcedureItemKind.Hint;

                case TestProcedureStepKind.Warning:
                    return Compiler.TestProcedureItemKind.Warning;

                case TestProcedureStepKind.Note:
                    return Compiler.TestProcedureItemKind.Note;

                default:
                    return Compiler.TestProcedureItemKind.Instruction;
            }
        }
    }
}
