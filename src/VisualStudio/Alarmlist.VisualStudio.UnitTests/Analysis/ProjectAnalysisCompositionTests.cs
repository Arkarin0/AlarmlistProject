// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.References;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public class ProjectAnalysisCompositionTests
    {
        [Fact]
        public void AlarmReferenceEditingOverridesCpsCompatibilityDefaultsOnlyForAlarmProjects()
        {
            Type checker = typeof(AlarmlistProjectReferenceChecker);
            ExportAttribute export = Assert.Single(checker.GetCustomAttributes<ExportAttribute>());
            Assert.Equal(typeof(IValidProjectReferenceChecker), export.ContractType);
            CustomAttributeData capability = Assert.Single(checker.CustomAttributes
                .Where(attribute => attribute.AttributeType == typeof(AppliesToAttribute)));
            Assert.Equal(AlarmlistProject.Capability, capability.ConstructorArguments[0].Value);
            // Precedence zero ties the CPS fallback checker, which can reject
            // alarm references before the native manager persists them.
            OrderAttribute order = Assert.Single(checker.GetCustomAttributes<OrderAttribute>());
            Assert.True(order.OrderPrecedence > 0);
        }

        [Fact]
        public void AnalysisIsExportedForCpsProjectDynamicLoading()
        {
            // CPS discovers dynamic load components through its named project
            // scope contract; an unnamed export silently leaves analysis dormant.
            ExportAttribute export = Assert.Single(typeof(AlarmlistProjectAnalysis).GetCustomAttributes<ExportAttribute>());
            Assert.Equal(ExportContractNames.Scopes.UnconfiguredProject, export.ContractName);
            Assert.Equal(typeof(IProjectDynamicLoadComponent), export.ContractType);
            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(AlarmlistProjectAnalysis)));
            CustomAttributeData capability = Assert.Single(typeof(AlarmlistProjectAnalysis).CustomAttributes
                .Where(attribute => attribute.AttributeType == typeof(AppliesToAttribute)));
            Assert.Equal(AlarmlistProject.Capability, capability.ConstructorArguments[0].Value);
        }
    }
}
