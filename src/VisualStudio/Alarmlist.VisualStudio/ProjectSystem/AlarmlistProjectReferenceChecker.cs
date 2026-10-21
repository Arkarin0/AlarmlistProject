// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.References;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export(typeof(IValidProjectReferenceChecker))]
    [AppliesTo(AlarmlistProject.Capability)]
    [Order(10)] // Ahead of the built-in CPS checker (precedence 0).
    internal sealed class AlarmlistProjectReferenceChecker : IValidProjectReferenceChecker
    {
        // Like the .NET project system, permit editing references here. Library
        // compatibility and graph validity belong to build/design-time analysis,
        // so XML edits and the native reference manager have the same semantics.
        public Task<SupportedCheckResult> CanAddProjectReferenceAsync(object referencedProject)
        {
            if (referencedProject == null) throw new ArgumentNullException(nameof(referencedProject));
            return Task.FromResult(SupportedCheckResult.Supported);
        }

        public Task<SupportedCheckResult> CanBeReferencedAsync(object referencingProject)
        {
            if (referencingProject == null) throw new ArgumentNullException(nameof(referencingProject));
            return Task.FromResult(SupportedCheckResult.Supported);
        }

        public Task<CanAddProjectReferencesResult> CanAddProjectReferencesAsync(IImmutableSet<object> referencedProjects)
        {
            if (referencedProjects == null) throw new ArgumentNullException(nameof(referencedProjects));
            var results = ImmutableDictionary.CreateBuilder<object, SupportedCheckResult>();
            foreach (object project in referencedProjects)
            {
                if (project == null) throw new ArgumentException("A reference cannot be null.", nameof(referencedProjects));
                results.Add(project, SupportedCheckResult.Supported);
            }

            return Task.FromResult(new CanAddProjectReferencesResult(results.ToImmutable(), null));
        }
    }
}
