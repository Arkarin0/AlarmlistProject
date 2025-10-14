using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
   public class AlarmData
    {
        public Guid ID { get; set; } = Guid.NewGuid();

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public AlarmSolutionList SolutionList { get; set; } = new AlarmSolutionList();
        

        public static IEnumerable<AlarmData> GenerateSampleData()
        {
            List<AlarmData> list = new List<AlarmData>();

            for (int i = 0; i < 1000; i++)
            {
                list.Add(new AlarmData()
                {
                    Code = i.ToString(),
                    Name = "Fehler" + i.ToString(),
                    Description = "Beschreibung" + i.ToString()
                });
            }
            return list;
        }
    }
}
