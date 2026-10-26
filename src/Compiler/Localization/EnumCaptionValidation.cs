// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Definition;
using Alarmlist.Diagnostics;

namespace Alarmlist.Localization
{
    internal static class EnumCaptionValidation
    {
        internal static IEnumerable<Diagnostic> Validate(IEnumerable<AlarmFieldDescriptor> fields, ResourceCatalogue resources, string culture)
        {
            foreach (string key in fields.Where(field => field.Type == "Enum").SelectMany(field => field.ValueLabelKeys)
                .Where(key => key != null).Distinct(StringComparer.Ordinal))
                if (!resources.TryResolve(key, culture, out _))
                    yield return new Diagnostic(DiagnosticDescriptors.MissingEnumCaption, key, culture);
        }
    }
}
