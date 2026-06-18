// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Alarmlist.MSBuild
{
    public abstract class BaseTask: Task
    {
        readonly Microsoft.Extensions.Logging.ILogger _logger;

        internal BaseTask() : base()
        {
            this._logger = new LoggingHelper(base.Log);
        }

        /// <summary>
        /// This is not a MSBuild property, it is only used in this class.
        /// </summary>
        public Microsoft.Extensions.Logging.ILogger ILogger { get { return _logger; } }

    }
}
