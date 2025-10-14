using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
    /// <summary>
    /// An errocode with the same code allready exists.
    /// </summary>
    public class DuplicateErrocodeException:Exception
    {
        /// <summary>
        /// The code which is missed.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A collection of items which share the same errocode..
        /// </summary>
        public IEnumerable<string> Dublicates { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoReferenceException"/> class.
        /// </summary>
        /// <param name="code">The code which is missed.</param>
        /// <param name="duplicates">A collection of items which share the same errocode.</param>
        public DuplicateErrocodeException(string code, IEnumerable<string> duplicates) :
            base("The Alarm Collection contians more then one items with the same code." +
                $"\nAlarm: {code}" +
                $"\nDublicates: {string.Join(", ", duplicates)}")
        {
            Code = code;
            Dublicates = duplicates;
        }
    }
}
