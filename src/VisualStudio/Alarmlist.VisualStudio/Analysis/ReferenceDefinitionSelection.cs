// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class ReferenceDefinitionTarget
    {
        internal ReferenceDefinitionTarget(int consumer, AnalysisLocation location) { Consumer = consumer; Location = location; }
        internal int Consumer { get; }
        internal AnalysisLocation Location { get; }
    }

    internal static class ReferenceDefinitionSelection
    {
        // Physical ownership comes from compiler-issued source-token identities,
        // never matching names or decoding package/library source paths.
        internal static IReadOnlyList<ReferenceDefinitionTarget> Get(SolutionReferenceSearchResult result, int origin)
        {
            if (result?.IsAvailable != true || result.Origin?.IsAvailable != true || result.Origin.IsAmbiguous
                || result.Origin.Symbol?.IsAmbiguous == true) return Array.Empty<ReferenceDefinitionTarget>();
            var declarations = result.Contexts.SelectMany(context => context.Families.SelectMany(family => family.Occurrences
                .Where(use => use.Role == "Declaration" && use.Location != null).Select(use => (context.Consumer, Use: use))));
            var selected = declarations.GroupBy(row => row.Use.SourceTokenId != null
                    ? (row.Use.SourceTokenId, (string)null, (int?)null, 0, 0, 0, 0)
                    : ((string)null, row.Use.Location.FilePath, row.Use.Location.SourceVersion, row.Use.Location.LineNumber,
                        row.Use.Location.ColumnNumber, row.Use.Location.EndLineNumber, row.Use.Location.EndColumnNumber))
                .Select(group => group.OrderByDescending(row => row.Use.Start.HasValue && row.Use.Unavailable == null)
                    .ThenBy(row => row.Consumer == origin ? 0 : 1).First())
                .Select(row => new ReferenceDefinitionTarget(row.Consumer, row.Use.Location)).ToArray();
            return Array.AsReadOnly(selected);
        }
    }
}
