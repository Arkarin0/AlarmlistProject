using System.Collections.Generic;
using Microsoft.Build.Framework;
using Moq;

namespace AlarmList.MSBuild.UnitTests
{
    public class TasksTestsBase
    {
        protected readonly Mock<IBuildEngine> buildEngine;
        protected readonly List<BuildErrorEventArgs> buildErrors;
        protected readonly List<BuildWarningEventArgs> buildWarnings;
        protected readonly List<BuildMessageEventArgs> buildMessages;


        public TasksTestsBase()
        {
            buildEngine = new Mock<IBuildEngine>();
            buildErrors = new List<BuildErrorEventArgs>();
            buildWarnings = new List<BuildWarningEventArgs>();
            buildMessages = new List<BuildMessageEventArgs>();
            buildEngine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => buildErrors.Add(e));
            buildEngine.Setup(x => x.LogWarningEvent(It.IsAny<BuildWarningEventArgs>())).Callback<BuildWarningEventArgs>(e => buildWarnings.Add(e));
            buildEngine.Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>())).Callback<BuildMessageEventArgs>(e => buildMessages.Add(e));
        }
    }
}
