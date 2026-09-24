// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    // The pinned CPS SDK exposes this post-save hook on both supported hosts.
    // It is required to reconcile native text-buffer Save As with project items.
#pragma warning disable CS0618
    [Export(typeof(IFileActionHandler))]
    [AppliesTo(AlarmlistProject.Capability)]
    internal sealed class AlarmlistFileSaveHandler : IFileActionHandler
#pragma warning restore CS0618
    {
        private readonly UnconfiguredProject _project;

        [ImportingConstructor]
        public AlarmlistFileSaveHandler(UnconfiguredProject project) { _project = project; }

        public async Task<bool> TryHandleFileSavedAsync(IProjectTree tree, string newFilePath, bool saveAs)
        {
            if (!(tree is IVsBrowseObjectContext context) || tree.IsFolder
                || !string.Equals(Path.GetExtension(tree.FilePath), ".almx", StringComparison.OrdinalIgnoreCase)) return false;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var rdt = (IVsRunningDocumentTable)ServiceProvider.GlobalProvider.GetService(typeof(SVsRunningDocumentTable));
            if (rdt == null) throw new InvalidOperationException("Visual Studio's document table is unavailable.");
            IntPtr data = IntPtr.Zero;
            IVsHierarchy hierarchy;
            string target;
            try
            {
                ErrorHandler.ThrowOnFailure(rdt.FindAndLockDocument(0, tree.FilePath, out hierarchy, out uint itemId, out data, out uint cookie));
                if (data == IntPtr.Zero || !(Marshal.GetObjectForIUnknown(data) is IPersistFileFormat persistence)) return false;
                ErrorHandler.ThrowOnFailure(persistence.GetCurFile(out target, out uint format));
            }
            finally { if (data != IntPtr.Zero) Marshal.Release(data); }

            // Native text persistence can change its filename but return no new
            // moniker to CPS. Use the successfully persisted filename as evidence.
            if (string.IsNullOrEmpty(target) || string.Equals(tree.FilePath, target, StringComparison.OrdinalIgnoreCase)) return false;
            IProjectItem source = await context.ConfiguredProject.Services.SourceItems.GetItemAsync(context.ProjectPropertiesContext);
            if (source == null) throw new InvalidOperationException("The saved ALMX file could not be associated with its project item.");
            string include = _project.MakeRelative(target);
            string oldInclude = source.EvaluatedInclude;
            string itemType = source.ItemType;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in await source.Metadata.GetDirectPropertyNamesAsync())
                metadata[name] = await source.Metadata.GetUnevaluatedPropertyValueAsync(name);
            if (Path.IsPathRooted(include) || include.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                metadata["Link"] = ProjectCollection.Escape(Path.GetFileName(target));
            else
                metadata.Remove("Link");
            // Publish the destination before transferring ownership. Renaming
            // the old item first can make CPS close its still-attached views.
            if (!(await context.ConfiguredProject.Services.SourceItems.GetItemsAsync(itemType, include)).Any())
                await context.ConfiguredProject.Services.SourceItems.AddAsync(itemType, ProjectCollection.Escape(include), metadata);
            IProjectItem destination = (await context.ConfiguredProject.Services.SourceItems.GetItemsAsync(itemType, include)).First();
            foreach (KeyValuePair<string, string> value in metadata)
                await destination.Metadata.SetPropertyValueAsync(value.Key, value.Value);
            if (!metadata.ContainsKey("Link")) await destination.Metadata.DeletePropertyAsync("Link");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ErrorHandler.ThrowOnFailure(((IVsProject3)hierarchy).TransferItem(tree.FilePath, target, null));
            // Save As preserves the old disk file. Exclude its globbed source
            // item so the project does not compile two copies of the same alarm.
            foreach (IProjectItem oldItem in (await context.ConfiguredProject.Services.SourceItems.GetItemsAsync(itemType, oldInclude)).ToArray())
                await oldItem.RemoveAsync(DeleteOptions.None);
            return true;
        }
    }
}
