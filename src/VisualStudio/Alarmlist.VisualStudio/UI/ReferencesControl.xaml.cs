// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows.Controls;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    public partial class ReferencesControl : UserControl
    {
        public ReferencesControl() { InitializeComponent(); }
        private void OnSymbolKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || !(DataContext is ReferenceResultsViewModel view)
                || !view.ChooseSymbolCommand.CanExecute(null)) return;
            view.ChooseSymbolCommand.Execute(null); args.Handled = true;
        }
        private void OnResultKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None
                || !(Results.SelectedItem is ReferenceResultNode row) || row.GoToSourceCommand?.CanExecute(null) != true) return;
            row.GoToSourceCommand.Execute(null); args.Handled = true;
        }
    }
}
