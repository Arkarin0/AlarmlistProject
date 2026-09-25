// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class InheritedValueAdorner : Adorner
    {
        public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(nameof(Brush), typeof(Brush),
            typeof(InheritedValueAdorner), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush Brush { get => (Brush)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

        public InheritedValueAdorner(Control control) : base(control)
        {
            IsHitTestVisible = false;
            Focusable = false;
            ClipToBounds = true;
            SetBinding(BrushProperty, new Binding(nameof(Control.Foreground)) { Source = control });
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // The inherited field reserves a 26-DIP left inset. Draw there, away
            // from selectable text and scrollbars, using its live theme brush.
            var pen = new Pen(Brush, 1.6);
            drawingContext.PushTransform(new TranslateTransform(13, 13));
            drawingContext.PushTransform(new RotateTransform(-40));
            drawingContext.DrawRoundedRectangle(null, pen, new Rect(-8, -3.5, 10, 7), 3.5, 3.5);
            drawingContext.DrawRoundedRectangle(null, pen, new Rect(-2, -3.5, 10, 7), 3.5, 3.5);
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }

    public static class InheritedValueBehavior
    {
        public static readonly DependencyProperty IsInheritedProperty = DependencyProperty.RegisterAttached("IsInherited", typeof(bool),
            typeof(InheritedValueBehavior), new PropertyMetadata(false, OnChanged));
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(AdornmentState),
            typeof(InheritedValueBehavior));
        public static bool GetIsInherited(DependencyObject value) => (bool)value.GetValue(IsInheritedProperty);
        public static void SetIsInherited(DependencyObject value, bool inherited) => value.SetValue(IsInheritedProperty, inherited);

        private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is Control control)) return;
            var state = (AdornmentState)control.GetValue(StateProperty);
            if (state == null)
            {
                state = new AdornmentState(control);
                control.SetValue(StateProperty, state);
            }
            state.Update();
        }

        private sealed class AdornmentState
        {
            private readonly Control _control;
            private AdornerLayer _layer;
            private InheritedValueAdorner _adorner;
            public AdornmentState(Control control)
            {
                _control = control;
                control.Loaded += OnLoaded;
                control.Unloaded += OnUnloaded;
                control.DataContextChanged += OnDataContextChanged;
            }
            private void OnLoaded(object sender, RoutedEventArgs args) => Update();
            private void OnUnloaded(object sender, RoutedEventArgs args) => Remove();
            private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args) { Remove(); Update(); }
            public void Update()
            {
                if (!_control.IsLoaded || !GetIsInherited(_control)) { Remove(); return; }
                if (_adorner != null) return;
                _layer = AdornerLayer.GetAdornerLayer(_control);
                if (_layer == null) return;
                _adorner = new InheritedValueAdorner(_control);
                _layer.Add(_adorner);
            }
            private void Remove()
            {
                if (_adorner != null) { _layer.Remove(_adorner); BindingOperations.ClearAllBindings(_adorner); }
                _adorner = null;
                _layer = null;
            }
        }
    }
}
