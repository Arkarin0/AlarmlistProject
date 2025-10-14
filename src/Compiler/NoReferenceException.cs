using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
    /// <summary>
    /// A Reference to an Alarm is missing.
    /// </summary>
    /// <seealso cref="Exception" />
    public class NoReferenceException:Exception
    {
        /// <summary>
        /// The code which is missed.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A collection of Items which depend on the missid errocode.
        /// </summary>
        public IEnumerable<string> RequestedBy { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoReferenceException"/> class.
        /// </summary>
        /// <param name="code">The code which is missed.</param>
        /// <param name="requestedBy">A collection of items which depend on the missid errocode</param>
        public NoReferenceException(string code, IEnumerable<string> requestedBy) :
            base("The Alarm Collection is missing a referece to an alarm." +
                $"\nAlarm: {code}" +
                $"\nDependancies: {string.Join(", ", requestedBy)}")
        {
            Code = code;
            RequestedBy = requestedBy;
        }
    }
}
