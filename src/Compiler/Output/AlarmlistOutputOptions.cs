using System;

namespace Alarmlist.Output
{
    public sealed class AlarmlistOutputOptions
    {
        public AlarmlistOutputOptions(string filePath, AlarmlistOutputFormat format = AlarmlistOutputFormat.Xml)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("An output file path must be provided.", nameof(filePath));

            FilePath = filePath;
            Format = format;
        }

        public string FilePath { get; }

        public AlarmlistOutputFormat Format { get; }
    }
}
