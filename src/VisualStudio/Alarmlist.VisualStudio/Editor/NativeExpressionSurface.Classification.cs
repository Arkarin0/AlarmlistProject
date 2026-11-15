// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed partial class NativeExpressionSurface
    {
        private JoinableTaskContext _classificationThreading;
        private JoinableTaskCollection _classificationTasks;
        private JoinableTaskFactory _classificationWork;
        private readonly CancellationTokenSource _classificationLifetime = new CancellationTokenSource();
        private CancellationToken _classificationToken;
        private CancellationTokenSource _classificationRequest;
        private ExpressionValueTags _tags;
        private ITextSnapshot _attemptedValue;
        private ExpressionValueContext _attemptedContext;
        private bool _attemptedResolved, _attemptedEnabled;
        private int _classificationQueued, _classificationGeneration;
        private readonly DependencyPropertyDescriptor[] _presentationProperties = new[]
        {
            DependencyPropertyDescriptor.FromProperty(ExpressionTextBox.IsExpressionEnabledProperty, typeof(ExpressionTextBox)),
            DependencyPropertyDescriptor.FromProperty(ExpressionTextBox.HasResolvedValueProperty, typeof(ExpressionTextBox)),
            DependencyPropertyDescriptor.FromProperty(ExpressionTextBox.RawTextProperty, typeof(ExpressionTextBox)),
            DependencyPropertyDescriptor.FromProperty(ExpressionTextBox.ResolvedTextProperty, typeof(ExpressionTextBox))
        };
        private void InitializeClassification(IClassificationTypeRegistryService types)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _classificationToken = _classificationLifetime.Token;
            _classificationThreading = ThreadHelper.JoinableTaskContext;
            _classificationTasks = _classificationThreading.CreateCollection(); _classificationWork = _classificationThreading.CreateFactory(_classificationTasks);
            _tags = ExpressionValueClassificationProvider.Get(_view, types);
            _view.TextBuffer.Changed += OnClassificationInput;
            _model.PropertyChanged += OnClassificationModel;
            _owner.DataContextChanged += OnClassificationPresentation; _owner.IsKeyboardFocusWithinChanged += OnClassificationPresentation;
            foreach (var property in _presentationProperties) property.AddValueChanged(_owner, OnClassificationContext);
            ScheduleClassification();
        }
        private void OnClassificationInput(object sender, TextContentChangedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ScheduleClassification(); }
        private void OnClassificationModel(object sender, PropertyChangedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ScheduleClassification(); }
        private void OnClassificationPresentation(object sender, DependencyPropertyChangedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ScheduleClassification(); }
        private void OnClassificationContext(object sender, EventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ScheduleClassification(); }
        private void ScheduleClassification()
        {
            if (_disposed || Interlocked.Exchange(ref _classificationQueued, 1) != 0) return;
            _ = _classificationWork.RunAsync(async () =>
            {
                try
                {
                    await Task.Yield(); await _classificationWork.SwitchToMainThreadAsync(_classificationToken);
                    Interlocked.Exchange(ref _classificationQueued, 0); RefreshClassification();
                }
                catch (OperationCanceledException) { }
                catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error))
                { Interlocked.Exchange(ref _classificationQueued, 0); Trace.TraceError("Alarmlist value classification unavailable: {0}", error); }
            });
        }
        private static bool SameContext(ExpressionValueContext left, ExpressionValueContext right)
            => left == null || right == null ? left == right : ReferenceEquals(left.State, right.State) && ReferenceEquals(left.Target, right.Target)
                && ReferenceEquals(left.Location, right.Location) && left.IsDraft == right.IsDraft;
        private void RefreshClassification()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (_disposed) return;
            var snapshot = _view.TextBuffer.CurrentSnapshot; var context = _model.ExpressionContext(_owner.DataContext);
            if (ReferenceEquals(snapshot, _attemptedValue) && SameContext(context, _attemptedContext)
                && _attemptedResolved == _owner.IsDisplayingResolved && _attemptedEnabled == _owner.IsExpressionEnabled) return;
            _attemptedValue = snapshot; _attemptedContext = context; _attemptedResolved = _owner.IsDisplayingResolved; _attemptedEnabled = _owner.IsExpressionEnabled;
            _classificationRequest?.Cancel(); _classificationRequest = null; _tags.Clear();
            if (context == null || _owner.IsDisplayingResolved || !_owner.IsExpressionEnabled || !(_owner.IsLoaded && _owner.IsVisible)) return;
            var target = new AnalysisClassificationTarget("designer:" + (++_classificationGeneration), "Value",
                new AnalysisInput(snapshot.GetText(), context.Location?.FilePath ?? context.Analysis.DocumentPath, snapshot.Version.VersionNumber),
                context.Target, context.Location, context.IsDraft);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_classificationToken); _classificationRequest = cancellation;
            _ = _classificationWork.RunAsync(async () =>
            {
                try
                {
                    var response = await ((IDesignerClassification)context.Analysis).ClassifyAsync(context.State, target, cancellation.Token);
                    await _classificationWork.SwitchToMainThreadAsync(cancellation.Token);
                    if (_disposed || !ReferenceEquals(_classificationRequest, cancellation) || !ReferenceEquals(snapshot, _view.TextBuffer.CurrentSnapshot)
                        || !SameContext(context, _model.ExpressionContext(_owner.DataContext)) || _owner.IsDisplayingResolved || !_owner.IsExpressionEnabled) return;
                    if (response.Availability == AnalysisAvailability.Available) _tags.Publish(snapshot, target, response.Result);
                }
                catch (OperationCanceledException) { }
                catch (Exception error) when (SdkAnalysisBridge.IsRecoverable(error)) { Trace.TraceError("Alarmlist value classification unavailable: {0}", error); }
                finally
                {
                    await _classificationWork.SwitchToMainThreadAsync();
                    if (ReferenceEquals(_classificationRequest, cancellation)) _classificationRequest = null;
                    cancellation.Dispose();
                }
            });
        }
        private void DisposeClassification()
        {
            _classificationLifetime.Cancel(); _classificationRequest = null;
            _view.TextBuffer.Changed -= OnClassificationInput; _model.PropertyChanged -= OnClassificationModel;
            _owner.DataContextChanged -= OnClassificationPresentation; _owner.IsKeyboardFocusWithinChanged -= OnClassificationPresentation;
            foreach (var property in _presentationProperties) property.RemoveValueChanged(_owner, OnClassificationContext);
            _classificationThreading.Factory.Run(_classificationTasks.JoinTillEmptyAsync);
            _tags.Clear(); _classificationLifetime.Dispose(); _attemptedValue = null; _attemptedContext = null;
        }
    }
}
