// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Extensibility;

namespace Alarmlist.Output
{
    /// <summary>Loads only explicitly declared provider entry types from captured bytes.</summary>
    public sealed class ExportProviderLoader : IDisposable
    {
        private readonly List<CapturedAssemblyScope> _scopes = new List<CapturedAssemblyScope>();
        private IReadOnlyList<IAlarmlistExportProvider> _providers;
        public IReadOnlyList<IAlarmlistExportProvider> Providers => _providers ?? throw new ObjectDisposedException(nameof(ExportProviderLoader));

        public ExportProviderLoader(IEnumerable<ExportProviderReference> references)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var captured = references.ToArray();
            if (captured.Any(reference => reference == null)) throw new ArgumentException("Provider references cannot be null.", nameof(references));
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "XML", "JSON" };
            foreach (var reference in captured)
                if (!ids.Add(reference.Id)) throw new ArgumentException("Duplicate or reserved export provider ID '" + reference.Id + "'.", nameof(references));
            var providers = new List<IAlarmlistExportProvider>();
            try
            {
                foreach (var reference in captured)
                {
                    var scope = new CapturedAssemblyScope(reference.Images.Select(image =>
                        new KeyValuePair<string, byte[]>(image.FilePath, image.GetImage())), "Export provider");
                    _scopes.Add(scope);
                    var assembly = scope.LoadRoot(reference.FilePath);
                    Type type = assembly.GetType(reference.TypeName, throwOnError: true, ignoreCase: false);
                    if (!type.IsVisible || type.IsAbstract || type.ContainsGenericParameters || !typeof(IAlarmlistExportProvider).IsAssignableFrom(type))
                        throw new NotSupportedException("Export provider entry type must be a public concrete IAlarmlistExportProvider: " + reference.TypeName);
                    var provider = (IAlarmlistExportProvider)Activator.CreateInstance(type);
                    providers.Add(provider);
                    if (!string.Equals(provider.Id, reference.Id, StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException("Export provider entry type ID does not match declared ID '" + reference.Id + "'.");
                    if (provider.ContractVersion != ExportDocument.ContractVersion)
                        throw new NotSupportedException("Export provider '" + reference.Id + "' has an incompatible contract. Rebuild the provider against the matching tooling.");
                }
                _providers = Array.AsReadOnly(providers.ToArray());
            }
            catch
            {
                _providers = Array.AsReadOnly(providers.ToArray());
                try { Dispose(); }
                catch (Exception exception) when (CapturedAssemblyScope.IsRecoverable(exception)) { }
                throw;
            }
        }

        public void Dispose()
        {
            if (_providers == null) return;
            var providers = _providers;
            _providers = null;
            Exception failure = null;
            foreach (var provider in providers)
                try { (provider as IDisposable)?.Dispose(); }
                catch (Exception exception) when (CapturedAssemblyScope.IsRecoverable(exception)) { failure = failure ?? exception; }
            foreach (var scope in _scopes)
                try { scope.Dispose(); }
                catch (Exception exception) when (CapturedAssemblyScope.IsRecoverable(exception)) { failure = failure ?? exception; }
            _scopes.Clear();
            if (failure != null) throw new InvalidOperationException("Export provider disposal failed: " + failure.Message, failure);
        }
    }
}
