using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
    /// <summary>
    /// A Circe dependancy exist. Which spans one or more Alarms.
    /// </summary>
    public class CircleDependancyException : Exception
    {
        /// <summary>
        /// The code which is missed.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A collection of items which result in a circle.
        /// </summary>
        public IEnumerable<string> CircleTrace { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoReferenceException"/> class.
        /// </summary>
        /// <param name="code">The code which is missed.</param>
        /// <param name="circletrace">A collection of items which result in a circle.</param>
        public CircleDependancyException(string code, IEnumerable<string> circletrace) :
            base("The Alarm Collection contains an alarm which dependancies result in a circular dependancy." +
                $"\nAlarm: {code}" +
                $"\nCircle: {string.Join(" -> ", circletrace)}")
        {
            Code = code;
            CircleTrace = circletrace;
        }
    }
}
