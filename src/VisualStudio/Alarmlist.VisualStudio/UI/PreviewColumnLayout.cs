// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class PreviewColumn : ObservableObject
    {
        private readonly Action _changed;
        private bool _visible = true;
        private double _width;
        private int _order;
        public string Id { get; }
        public string Label => Id == "TestMethod" ? "Test method" : Id;
        public double DefaultWidth { get; }
        public int Order { get => _order; internal set { _order = value; Notify(); Notify(nameof(Position)); } }
        public int Position => Order + 1;
        public bool IsVisible { get => _visible; set { if (_visible == value) return; _visible = value; Notify(); _changed(); } }
        public double Width
        {
            get => _width;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double width = Math.Max(40, Math.Min(2000, value));
                if (Math.Abs(_width - width) < 0.1) return;
                _width = width; Notify(); _changed();
            }
        }
        public DelegateCommand MoveLeftCommand { get; }
        public DelegateCommand MoveRightCommand { get; }
        public PreviewColumn(string id, double width, int order, Action changed, Action<PreviewColumn, int> move)
        {
            Id = id; DefaultWidth = _width = width; _order = order; _changed = changed;
            MoveLeftCommand = new DelegateCommand(() => move(this, -1), () => Order > 0);
            MoveRightCommand = new DelegateCommand(() => move(this, 1), () => Order < 6);
        }
        internal void RefreshActions() { MoveLeftCommand.Refresh(); MoveRightCommand.Refresh(); }
    }

    internal sealed class PreviewColumnLayout : ObservableObject, IDisposable
    {
        private ProjectUserSetting _store;
        private bool _applying;
        private bool _disposed;
        public IReadOnlyList<PreviewColumn> Columns { get; }
        public IEnumerable<PreviewColumn> OrderedColumns => Columns.OrderBy(column => column.Order);
        public string Status => _store?.Error ?? (_store == null ? "Column layout is available for this document session." : "Column preferences apply to this project and user.");
        public string Error => _store?.Error;
        public DelegateCommand ResetCommand { get; }
        public Task PendingLoad { get; private set; } = Task.CompletedTask;
        public Task PendingSave => _store?.PendingSave ?? Task.CompletedTask;
        public event EventHandler Changed;
        public PreviewColumnLayout()
        {
            string[] ids = { "Name", "Code", "Category", "Description", "TestMethod", "Identity", "Source" };
            double[] widths = { 200, 100, 130, 300, 420, 220, 180 };
            Columns = BindingCollection.Create(ids.Select((id, index) => new PreviewColumn(id, widths[index], index, Commit, Move)));
            ResetCommand = new DelegateCommand(Reset);
        }
        public void SetStore(ProjectUserSetting store)
        {
            if (_disposed || ReferenceEquals(_store, store)) return;
            if (_store != null) _store.Changed -= OnStoreChanged;
            _store = store;
            Apply(store?.IsLoaded == true ? store.Value : "");
            if (store != null)
            {
                store.Changed += OnStoreChanged;
                PendingLoad = store.LoadAsync();
            }
            Notify(nameof(Status)); Notify(nameof(Error));
        }
        private void OnStoreChanged(object sender, EventArgs args)
        {
            if (_disposed || !ReferenceEquals(sender, _store)) return;
            if (_store.IsLoaded && Serialize() != _store.Value) Apply(_store.Value);
            Notify(nameof(Status)); Notify(nameof(Error));
        }
        internal string Serialize() => "1;" + string.Join(";", OrderedColumns.Select(column => column.Id + "," + (column.IsVisible ? "1" : "0")
            + "," + column.Width.ToString("0.##", CultureInfo.InvariantCulture)));
        internal void Apply(string value)
        {
            _applying = true;
            try
            {
                ApplyDefaults();
                if (!string.IsNullOrEmpty(value) && value.Length < 16384 && value.StartsWith("1;", StringComparison.Ordinal))
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    int order = 0;
                    bool valid = true;
                    foreach (string record in value.Substring(2).Split(';'))
                    {
                        string[] parts = record.Split(',');
                        PreviewColumn column = Columns.FirstOrDefault(item => item.Id == parts[0]);
                        if (column == null) continue; // Future columns do not invalidate known preferences.
                        if (parts.Length != 3 || !seen.Add(column.Id) || (parts[1] != "0" && parts[1] != "1")
                            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                            || double.IsNaN(width) || double.IsInfinity(width) || width < 40 || width > 2000)
                        { valid = false; break; }
                        column.Order = order++; column.IsVisible = parts[1] == "1"; column.Width = width;
                    }
                    if (!valid) ApplyDefaults();
                    else foreach (PreviewColumn column in Columns.Where(column => !seen.Contains(column.Id))) column.Order = order++;
                }
            }
            finally { _applying = false; }
            Publish();
        }
        private void ApplyDefaults()
        {
            for (int index = 0; index < Columns.Count; index++)
            { Columns[index].Order = index; Columns[index].IsVisible = true; Columns[index].Width = Columns[index].DefaultWidth; }
        }
        private void Reset() { Apply(""); Commit(); }
        private void Move(PreviewColumn column, int delta)
        {
            PreviewColumn other = Columns.FirstOrDefault(item => item.Order == column.Order + delta);
            if (other == null) return;
            int previous = column.Order; column.Order = other.Order; other.Order = previous; Commit();
        }
        public void SetOrder(IEnumerable<string> ids)
        {
            string[] order = ids.ToArray();
            if (order.Length != Columns.Count || order.Distinct().Count() != Columns.Count || order.Any(id => !Columns.Any(column => column.Id == id))) return;
            foreach (PreviewColumn column in Columns) column.Order = Array.IndexOf(order, column.Id);
            Commit();
        }
        public void SetWidths(IReadOnlyDictionary<string, double> widths)
        {
            _applying = true;
            try { foreach (PreviewColumn column in Columns) if (widths.TryGetValue(column.Id, out double width)) column.Width = width; }
            finally { _applying = false; }
            Commit();
        }
        private void Commit()
        {
            if (_applying || _disposed) return;
            _store?.Set(Serialize()); Publish();
        }
        private void Publish() { foreach (PreviewColumn column in Columns) column.RefreshActions(); Changed?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { _disposed = true; if (_store != null) _store.Changed -= OnStoreChanged; _store = null; Changed = null; }
    }
}
