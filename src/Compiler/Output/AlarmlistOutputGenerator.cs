using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;

namespace Alarmlist.Output
{
    public sealed class AlarmlistOutputGenerator
    {
        private readonly IReadOnlyList<IAlarmlistOutputWriter> _writers;

        public AlarmlistOutputGenerator()
            : this(new IAlarmlistOutputWriter[] { new XmlAlarmlistOutputWriter() })
        {
        }

        public AlarmlistOutputGenerator(IEnumerable<IAlarmlistOutputWriter> writers)
        {
            if (writers == null)
                throw new ArgumentNullException(nameof(writers));

            _writers = writers.ToArray();
        }

        public void Write(AlarmList alarmList, AlarmlistOutputOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var writer = _writers.FirstOrDefault(item => item.Format == options.Format);
            if (writer == null)
                throw new NotSupportedException($"The output format '{options.Format}' is not supported.");

            writer.Write(alarmList, options);
        }
    }
}
