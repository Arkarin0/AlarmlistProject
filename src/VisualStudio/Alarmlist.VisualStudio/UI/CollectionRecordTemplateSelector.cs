// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Alarmlist.VisualStudio.UI
{
    public sealed class CollectionRecordTemplateSelector : DataTemplateSelector
    {
        private readonly Dictionary<string, ResourceDictionary> _resources = new Dictionary<string, ResourceDictionary>(StringComparer.Ordinal);
        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!(item is CollectionPreviewRecord record) || record.Definition.TemplateResource == null) return DefaultTemplate;
            try
            {
                string path = record.Definition.TemplateResource;
                if (!_resources.TryGetValue(path, out ResourceDictionary dictionary))
                {
                    dictionary = (ResourceDictionary)Application.LoadComponent(new Uri("/Alarmlist.VisualStudio;component/" + path, UriKind.Relative));
                    _resources.Add(path, dictionary);
                }
                if (!(dictionary[record.Definition.TemplateKey] is DataTemplate template))
                    throw new InvalidDataException("The selected resource is not a DataTemplate.");
                record.TemplateError = null;
                return template;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is XamlParseException || error is ArgumentException
                || error is InvalidOperationException || error is InvalidCastException)
            {
                record.TemplateError = "Summary template unavailable; using default layout. " + error.Message;
                return DefaultTemplate;
            }
        }
    }
}
