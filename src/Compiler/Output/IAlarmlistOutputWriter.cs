using Alarmlist.Compiler;

namespace Alarmlist.Output
{
    public interface IAlarmlistOutputWriter
    {
        AlarmlistOutputFormat Format { get; }

        void Write(AlarmList alarmList, AlarmlistOutputOptions options);
    }
}
