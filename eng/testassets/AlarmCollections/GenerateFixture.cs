using System.IO;
using System.Reflection;

// Test-only access to generation before the coordinated runtime/host capability
// gate is lifted. Production generation continues to reject these definitions.
internal static class GenerateFixture
{
    public static void Main(string[] args)
    {
        var assembly = Assembly.Load("AlarmDefinitionGenerator");
        var definition = assembly.GetType("Alarmlist.Tools.AlarmDefinitionGenerator.Definition")
            .GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { args[0] });
        string source = (string)assembly.GetType("Alarmlist.Tools.AlarmDefinitionGenerator.Program")
            .GetMethod("Generate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { definition, (object)false });
        File.WriteAllText(args[1], source);
    }
}
