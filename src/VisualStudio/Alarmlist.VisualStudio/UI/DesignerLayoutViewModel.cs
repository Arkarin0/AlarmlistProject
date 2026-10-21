// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Globalization;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class DesignerLayoutViewModel : ObservableObject
    {
        private readonly IEditorLayoutSettings _settings;
        private bool _fileExpanded, _alarmExpanded;
        private double _fileProportion;
        public bool FileExpanded { get => _fileExpanded; set { if (_fileExpanded == value) return; _fileExpanded = value; Save(nameof(FileExpanded), value.ToString()); Notify(); } }
        public bool AlarmExpanded { get => _alarmExpanded; set { if (_alarmExpanded == value) return; _alarmExpanded = value; Save(nameof(AlarmExpanded), value.ToString()); Notify(); } }
        public double FileProportion
        {
            get => _fileProportion;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double proportion = Math.Max(0.1, Math.Min(0.8, value));
                if (Math.Abs(_fileProportion - proportion) < 0.0001) return;
                _fileProportion = proportion;
                Save(nameof(FileProportion), proportion.ToString(CultureInfo.InvariantCulture)); Notify();
            }
        }
        public DesignerLayoutViewModel(IEditorLayoutSettings settings)
        {
            _settings = settings;
            _fileExpanded = !bool.TryParse(Read(nameof(FileExpanded)), out bool file) || file;
            _alarmExpanded = !bool.TryParse(Read(nameof(AlarmExpanded)), out bool alarm) || alarm;
            _fileProportion = double.TryParse(Read(nameof(FileProportion)), NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio)
                && !double.IsNaN(ratio) && !double.IsInfinity(ratio) ? Math.Max(0.1, Math.Min(0.8, ratio)) : 0.25;
        }
        private string Read(string name) => _settings?.Read("Designer" + name, "");
        private void Save(string name, string value) => _settings?.Write("Designer" + name, value);
    }
}
