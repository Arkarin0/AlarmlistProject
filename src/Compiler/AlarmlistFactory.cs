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
            var values = Definition.AlarmDefinition.Fields.Select(field =>
            {
                string raw = node.GetEffectiveFieldValue(field.Name);
                return Definition.AlarmFieldValue.Resolve(field, raw, raw ?? field.Default, false, raw == null && field.Default != null);
            }).ToArray();
            if (values.Any(value => value.State == Definition.AlarmFieldValueState.Invalid))
                throw new ArgumentException("The syntax node contains invalid scalar values. Use AlarmCompiler to evaluate expressions and report diagnostics.", nameof(node));
            var alarm = new Compiler.Alarm(node.FullyQualifiedName, Array.AsReadOnly(values), CreateTestProcedureFromSyntaxNode(node.ResolvedTestProcedures));


            return alarm;
        }

        private static Compiler.TestProcedure CreateTestProcedureFromSyntaxNode(TestProcedureSyntax testProcedure)
        {
            return new Compiler.TestProcedure(testProcedure?.Groups.Select(group =>
                new Compiler.ProcedureGroup(group.Name, group.Title, group.Entries.Select(CreateTestProcedureItemFromSyntaxNode))));
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
