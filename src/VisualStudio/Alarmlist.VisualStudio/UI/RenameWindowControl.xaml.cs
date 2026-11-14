// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows.Controls;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    public partial class RenameWindowControl : UserControl
    {
        public RenameWindowControl() { InitializeComponent(); }
        private void OnSymbolKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || !(DataContext is ReferenceResultsViewModel view)
                || !view.ChooseSymbolCommand.CanExecute(null)) return;
            view.ChooseSymbolCommand.Execute(null); args.Handled = true;
        }
    }
}
