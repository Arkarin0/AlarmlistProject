// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;

namespace Alarmlist.VisualStudio.UI
{
    internal enum EditorMode { Designer, Split, XML }

    internal interface IEditorLayoutSettings
    {
        string Read(string name, string fallback);
        void Write(string name, string value);
    }

    [Export(typeof(IEditorLayoutSettings))]
    internal sealed class EditorLayoutSettings : IEditorLayoutSettings
    {
        private const string Collection = "Alarmlist/Editor";
        private readonly System.IServiceProvider _services;
        [ImportingConstructor]
        public EditorLayoutSettings([Import(typeof(SVsServiceProvider))] System.IServiceProvider services) { _services = services; }
        private WritableSettingsStore Store
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return new ShellSettingsManager(_services).GetWritableSettingsStore(SettingsScope.UserSettings);
            }
        }
        public string Read(string name, string fallback)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WritableSettingsStore store = Store;
            return store.PropertyExists(Collection, name) ? store.GetString(Collection, name) : fallback;
        }
        public void Write(string name, string value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WritableSettingsStore store = Store;
            if (!store.CollectionExists(Collection)) store.CreateCollection(Collection);
            store.SetString(Collection, name, value);
        }
    }

    internal sealed class EditorLayoutViewModel : ObservableObject
    {
        private readonly IEditorLayoutSettings _settings;
        private readonly Func<bool> _commit;
        private EditorMode _mode;
        private bool _sideBySide;
        private bool _swapped;
        private double _proportion;
        public EditorMode Mode => _mode;
        public bool IsDesigner => _mode == EditorMode.Designer;
        public bool IsSplit => _mode == EditorMode.Split;
        public bool IsXml => _mode == EditorMode.XML;
        public bool SideBySide { get => _sideBySide; set { _sideBySide = value; _settings.Write(nameof(SideBySide), value.ToString()); Notify(); } }
        public bool Swapped { get => _swapped; set { _swapped = value; _settings.Write(nameof(Swapped), value.ToString()); Notify(); } }
        public double Proportion
        {
            get => _proportion;
            set { _proportion = Math.Max(0.15, Math.Min(0.85, value)); _settings.Write(nameof(Proportion), _proportion.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        }
        public DelegateCommand DesignerCommand { get; }
        public DelegateCommand SplitCommand { get; }
        public DelegateCommand XmlCommand { get; }
        public DelegateCommand SwapCommand { get; }
        public EditorLayoutViewModel(IEditorLayoutSettings settings, Func<bool> commit, bool xmlOnly)
        {
            _settings = settings;
            _commit = commit;
            _mode = Enum.TryParse(settings.Read(nameof(Mode), "Split"), out EditorMode mode) && Enum.IsDefined(typeof(EditorMode), mode) ? mode : EditorMode.Split;
            if (xmlOnly) _mode = EditorMode.XML;
            _sideBySide = bool.TryParse(settings.Read(nameof(SideBySide), "False"), out bool sideBySide) && sideBySide;
            _swapped = bool.TryParse(settings.Read(nameof(Swapped), "False"), out bool swapped) && swapped;
            _proportion = double.TryParse(settings.Read(nameof(Proportion), "0.5"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double proportion)
                && !double.IsNaN(proportion) ? Math.Max(0.15, Math.Min(0.85, proportion)) : 0.5;
            DesignerCommand = new DelegateCommand(() => SetMode(EditorMode.Designer));
            SplitCommand = new DelegateCommand(() => SetMode(EditorMode.Split));
            XmlCommand = new DelegateCommand(() => SetMode(EditorMode.XML));
            SwapCommand = new DelegateCommand(() => Swapped = !Swapped);
        }
        public void SetMode(EditorMode mode)
        {
            if (_mode == mode) return;
            if (!_commit())
            {
                // Keep the draft/error visible while still allowing XML repair.
                if (mode == EditorMode.Designer) return;
                mode = EditorMode.Split;
            }
            _mode = mode;
            _settings.Write(nameof(Mode), mode.ToString());
            Notify(nameof(Mode)); Notify(nameof(IsDesigner)); Notify(nameof(IsSplit)); Notify(nameof(IsXml));
        }
    }
}
