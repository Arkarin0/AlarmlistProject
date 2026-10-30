// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Alarmlist.VisualStudio.UI;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(IWpfTextViewMarginProvider)), Name(XmlProjectContextMargin.Name)]
    [ContentType("XML"), TextViewRole(PredefinedTextViewRoles.Document)]
    [MarginContainer(PredefinedMarginNames.Top)]
    internal sealed class XmlProjectContextMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly ITextDocumentFactoryService _documents;
        private readonly VsDesignerAnalysisFactory _analysis;
        [ImportingConstructor]
        public XmlProjectContextMarginProvider(ITextDocumentFactoryService documents, VsDesignerAnalysisFactory analysis)
        { _documents = documents; _analysis = analysis; }
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost host, IWpfTextViewMargin container)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_documents.TryGetTextDocument(host.TextView.TextBuffer, out ITextDocument document)) return null;
            string extension = Path.GetExtension(document.FilePath);
            if (string.Equals(extension, ".almx", StringComparison.OrdinalIgnoreCase))
                return new XmlProjectContextMargin(host.TextView, _analysis.Create(host.TextView.TextBuffer));
            if (!XmlReferenceCommands.SupportsExtension(extension)) return null;
            return new XmlProjectContextMargin(host.TextView, _analysis.CreateReferenceContext(host.TextView.TextBuffer), hideWhenUnowned: true);
        }
    }

    internal sealed class XmlProjectContextMargin : IWpfTextViewMargin
    {
        internal const string Name = "Alarmlist.XmlProjectContext";
        private readonly IWpfTextView _view;
        private readonly IDesignerAnalysis _analysis;
        private readonly Border _element;
        private readonly ComboBox _choice;
        private readonly TextBlock _status;
        private bool _refreshing;
        private bool _disposed;
        private Choice[] _shown = Array.Empty<Choice>();
        private string _shownSelected;
        private bool _initialized;
        private readonly bool _hideWhenUnowned;

        internal XmlProjectContextMargin(IWpfTextView view, IDesignerAnalysis analysis, bool hideWhenUnowned = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _view = view; _analysis = analysis;
            _hideWhenUnowned = hideWhenUnowned;
            _choice = new ComboBox { MinWidth = 260, MaxWidth = 720, DisplayMemberPath = nameof(Choice.Label),
                SelectedValuePath = nameof(Choice.Path), ToolTip = "Choose the consumer that supplies analysis for this shared file." };
            AutomationProperties.SetName(_choice, "XML project context");
            AutomationProperties.SetAutomationId(_choice, Name);
            _choice.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ComboBoxStyleKey);
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(Choice.Label))));
            _choice.ItemContainerStyle = itemStyle;
            _choice.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            _choice.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            var label = new Label { Content = "_Project context", Target = _choice, Padding = new Thickness(0, 3, 10, 0) };
            label.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(label); panel.Children.Add(_choice);
            _status = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _status.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            AutomationProperties.SetAutomationId(_status, "Alarmlist.XmlAnalysisStatus");
            panel.Children.Add(_status);
            _element = new Border { Child = panel, Padding = new Thickness(6, 3, 6, 3) };
            _element.SetResourceReference(Border.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            _choice.SelectionChanged += OnSelection;
            _analysis.Changed += OnChanged;
            _view.Closed += OnClosed;
            Refresh();
        }

        private void OnChanged(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Refresh();
        }
        private void Refresh()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed || _refreshing) return;
            _refreshing = true;
            try
            {
                var projects = _analysis.Projects;
                _element.Visibility = _hideWhenUnowned && projects.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                string selected = _analysis.SelectedProjectPath;
                var state = _analysis.Current;
                _status.Text = selected == null ? "" : state?.Status == ProjectAnalysisStatus.Pending ? "Analyzing project"
                    : state?.Status != ProjectAnalysisStatus.Available || state.Response?.Snapshot?.ToolingFingerprint != AlarmDefinition.ToolingFingerprint
                        ? "Project analysis is unavailable" : "";
                _status.ToolTip = state?.Message;
                AutomationProperties.SetHelpText(_status, state?.Message ?? "");
                _status.Visibility = _status.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                var choices = projects.Select(project => new Choice(project.ProjectPath, new AnalysisProjectChoice(project).Label)).ToList();
                if (selected == null || choices.Count > 1) choices.Insert(0, new Choice(null,
                    projects.Count == 0 ? "No owning Alarmlist project" : "Choose a project context"));
                if (_initialized && string.Equals(_shownSelected, selected, StringComparison.OrdinalIgnoreCase)
                    && _shown.Select(choice => choice.Path + "\0" + choice.Label).SequenceEqual(choices.Select(choice => choice.Path + "\0" + choice.Label), StringComparer.Ordinal)) return;
                _shown = choices.ToArray(); _shownSelected = selected; _initialized = true;
                _choice.ItemsSource = choices;
                _choice.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Path, selected, StringComparison.OrdinalIgnoreCase));
                _choice.IsEnabled = projects.Count > 0;
            }
            finally { _refreshing = false; }
        }
        private void OnSelection(object sender, SelectionChangedEventArgs args)
        {
            if (!_refreshing && !_disposed && _choice.SelectedItem is Choice choice) _analysis.SelectedProjectPath = choice.Path;
        }
        private void OnClosed(object sender, EventArgs args) => Dispose();
        public FrameworkElement VisualElement => _element;
        public double MarginSize => _element.ActualHeight;
        public bool Enabled => !_disposed;
        public ITextViewMargin GetTextViewMargin(string marginName) => string.Equals(Name, marginName, StringComparison.OrdinalIgnoreCase) ? this : null;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.Closed -= OnClosed; _choice.SelectionChanged -= OnSelection; _analysis.Changed -= OnChanged;
            _analysis.Dispose(); _choice.ItemsSource = null;
        }
        private sealed class Choice
        {
            public string Path { get; }
            public string Label { get; }
            public Choice(string path, string label) { Path = path; Label = label; }
            public override string ToString() => Label;
        }
    }
}
