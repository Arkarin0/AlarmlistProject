// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Binding;
using Alarmlist.Text;

namespace Alarmlist.Core.UnitTests.Fakes
{
    public class BinderMock: Binder
    {
        public BinderMock(): base()
        { }

        public ICollection<SourceText> GetSourceTexts()
        {
            return sourceTexts;
        }
    }
}
