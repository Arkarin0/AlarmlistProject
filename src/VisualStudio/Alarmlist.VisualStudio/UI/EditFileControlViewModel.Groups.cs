// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        private string _newGroupName = "";
        public string NewGroupName { get => _newGroupName; set { if (_newGroupName == (value ?? "")) return; _newGroupName = value ?? ""; _draftVersion++; Notify(); AddGroupCommand?.Refresh(); } }
        public DelegateCommand AddGroupCommand { get; private set; }

        private void InitializeGroups()
        {
            AddGroupCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
            {
                string name = NewGroupName.Trim();
                if (!ProcedureSectionSource.IsValidName(name))
                    _error = "Enter a group ID starting with a letter or underscore; use letters, digits, dots, underscores or hyphens.";
                else if (alarm.ProcedureSections.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase)))
                    _error = "That group already exists. Edit its title or entries instead.";
                else if (_model.AddProcedureGroup(_original, name, out _error))
                {
                    NewGroupName = "";
                    FocusGroup(name);
                }
                Notify(nameof(Error));
            }), () => CanUseAnalysis && CurrentAlarm()?.IsResolved == true && NewGroupName.Trim().Length != 0);
        }

        private void InitializeGroupCommands(ProcedureSectionViewModel section)
        {
            section.TitleField.UseInheritedCommand = new DelegateCommand(() =>
            {
                if (!ProcedureSections.Contains(section) || !section.CanEditTitle) return;
                section.TitleField.IsPresent = false;
                CommitPending();
            }, () => CanEdit && section.CanEditTitle && section.TitleField.IsPresent);
            section.TitleField.GoToSourceCommand = new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
                Navigate(state, GetSection(alarm, section.Name)?.Title.ValueLocation)),
                () => CanUseAnalysis && GetSection(CurrentAlarm(), section.Name)?.Title.ValueLocation != null);
            section.RemoveGroupCommand = new DelegateCommand(() =>
            {
                if (!ProcedureSections.Contains(section) || !CommitPending() || !CanEdit) return;
                int index = ProcedureSections.IndexOf(section);
                if (_model.RemoveProcedureGroup(_original, section.Name, out _error))
                    FocusGroup(ProcedureSections.ElementAtOrDefault(Math.Min(index, ProcedureSections.Count - 1))?.Name);
                Notify(nameof(Error));
            }, () => CanEdit && !_analysisBusy && ProcedureSections.Contains(section)
                && _original?.GetProcedureSection(section.Name).Source != null && section.Error == null);
            section.MoveGroupUpCommand = MoveGroupCommand(section, -1);
            section.MoveGroupDownCommand = MoveGroupCommand(section, 1);
        }

        private DelegateCommand MoveGroupCommand(ProcedureSectionViewModel section, int direction)
            => new DelegateCommand(() => StartAnalysisAction((state, alarm) =>
            {
                string other = AdjacentLocalGroup(alarm, section.Name, direction);
                if (other != null && _model.MoveProcedureGroup(_original, section.Name, other, out _error)) FocusGroup(section.Name);
                Notify(nameof(Error));
            }), () => CanUseAnalysis && AdjacentLocalGroup(CurrentAlarm(), section.Name, direction) != null);

        private static string AdjacentLocalGroup(AnalysisAlarm alarm, string name, int direction)
        {
            if (alarm?.IsResolved != true) return null;
            string[] names = alarm.ProcedureSections.Where(group => group.IsLocallyDeclared && group.DefiningAlarm?.Index == alarm.Origin.Index)
                .Select(group => group.Name).ToArray();
            int index = Array.FindIndex(names, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            return index < 0 || index + direction < 0 || index + direction >= names.Length ? null : names[index + direction];
        }

        private void FocusGroup(string name)
        {
            ProcedureSectionViewModel section = ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            ProcedureFocus = new ProcedureFocusRequest(_selected.Source, section, -2);
        }
    }
}
