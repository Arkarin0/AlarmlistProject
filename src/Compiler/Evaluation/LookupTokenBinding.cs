// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using Alarmlist.Lookups;
using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    // Shared by normal evaluation and opt-in semantic reference capture.
    internal sealed class LookupTokenBinding
    {
        internal LookupPath Path { get; private set; }
        internal LookupMember Member { get; private set; }
        internal LookupMetadata Metadata { get; private set; }
        internal LookupListEntry Entry { get; private set; }
        internal SourceLocation Definition { get; private set; }
        internal string Failure { get; private set; }
        internal string[] Keys { get; private set; }

        internal static LookupTokenBinding Resolve(ExpressionToken token, LookupCatalogue catalogue)
        {
            var result = new LookupTokenBinding { Keys = token.Key.Split('#') };
            string[] keys = result.Keys;
            if (token.Kind == "list")
            {
                if (!catalogue.TryGetEntry(keys[0], keys[1], out var entry))
                    result.Failure = "missing or ambiguous list entry '" + token.Key + "'";
                else { result.Entry = entry; result.Definition = entry.Location; }
                return result;
            }
            if (!catalogue.TryGetPath(keys[0], out var path))
            {
                result.Failure = "missing or ambiguous path '" + keys[0] + "'";
                return result;
            }
            result.Path = path; result.Definition = path.Location;
            if (token.Kind == "path") return result;
            var member = path.Members.FirstOrDefault(candidate => candidate.Key == keys[1]);
            if (member == null || (token.Kind == "property" && member.Kind != "Property") || (token.Kind == "button" && member.Kind != "Button"))
            {
                result.Failure = "missing member or incorrect member kind '" + keys[1] + "'";
                return result;
            }
            result.Member = member; result.Definition = member.Location;
            if (token.Kind == "meta")
            {
                var metadata = member.Metadata.FirstOrDefault(candidate => candidate.Name == keys[2]);
                if (metadata == null) result.Failure = "missing metadata '" + keys[2] + "'";
                else { result.Metadata = metadata; result.Definition = metadata.Location; }
            }
            return result;
        }
    }
}
