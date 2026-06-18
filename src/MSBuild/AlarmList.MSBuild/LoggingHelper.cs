using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.MSBuild
{
    internal class LoggingHelper : ILogger
    {
        TaskLoggingHelper _logger;

        public LoggingHelper(TaskLoggingHelper logger) 
        { 
            _logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            //return _logger.IsTaskInputLoggingEnabled;
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var text= formatter(state,exception);
            var empty= string.Empty;

            switch (logLevel)
            {
                case LogLevel.Trace:
                    _logger.LogMessage(Microsoft.Build.Framework.MessageImportance.Normal, text);
                    break;
                case LogLevel.Debug:
                    _logger.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, text);
                    break;
                case LogLevel.Information:
                    _logger.LogMessage(Microsoft.Build.Framework.MessageImportance.High, text);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(text);
                    break;
                case LogLevel.Error:
                    _logger.LogError(text);
                    break;
                case LogLevel.Critical:
                    _logger.LogCriticalMessage(eventId.Name,empty,empty,empty,0,0,0,0,text);
                    break;
                case LogLevel.None:
                    break;
                default:
                    break;
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            {
            }
        }
    }
}
