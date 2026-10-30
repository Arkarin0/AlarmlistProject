// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ReferenceSymbolChoice : ObservableObject
    {
        internal ReferenceSymbolChoice(AnalysisReferenceSymbol symbol)
        {
            Symbol = symbol;
            Label = symbol.Kind + ": " + symbol.Name + (symbol.IsReadOnly ? " (read-only definition)" : "")
                + (symbol.IsAmbiguous ? " (ambiguous binding)" : "")
                + (symbol.Location == null ? "" : " · " + symbol.Location.FilePath + ":" + symbol.Location.LineNumber);
        }
        internal AnalysisReferenceSymbol Symbol { get; }
        public string Label { get; }
    }
}
