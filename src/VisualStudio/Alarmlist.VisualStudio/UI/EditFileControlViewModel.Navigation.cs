// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        internal object SelectSource(int position, string expectedText)
        {
            if (_model.Projection.Text != expectedText)
                throw new InvalidOperationException("The source changed while opening. Wait for analysis and try again.");
            if (HasPending)
                throw new InvalidOperationException("Commit or cancel the destination editor's draft before navigating.");
            DesignerSourceTarget target = DesignerSourceTarget.Find(_model.Projection, position);
            if (target == null) return null;
            if (!target.IsFile)
            {
                Filter = string.Empty;
                Selected = Alarms.Single(alarm => ReferenceEquals(alarm.Source, target.Alarm));
                DesignerLayout.AlarmExpanded = true;
            }
            else DesignerLayout.FileExpanded = true;
            if (target.Parameter != null)
            {
                ParameterListViewModel list = target.IsFile ? FileParameters : AlarmParameters;
                ParameterViewModel row = list.Rows.SingleOrDefault(item => item.Name == target.Parameter);
                list.Selected = row;
                return row;
            }
            if (target.Section != null)
            {
                ProcedureSectionViewModel section = ProcedureSections.Single(item => string.Equals(item.Name, target.Section, StringComparison.OrdinalIgnoreCase));
                if (target.Field == "Title") return section.TitleField;
                section.ShowCleared = true;
                return section.Items[target.Entry];
            }
            return Fields.Single(field => field.Name == target.Field);
        }
    }
}
