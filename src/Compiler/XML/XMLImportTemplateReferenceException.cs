using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.XML
{
    /// <summary>
    /// A Referenced XML ErrorcodeList File caused an exception.
    /// </summary>
    public class XMLImportTemplateReferenceException : Exception
    {
        /// <summary>
        /// The file that caused an exception.
        /// </summary>
        public string File { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="XMLImportTemplateReferenceException"/> class.
        /// </summary>
        /// <param name="file">The file that caused an exception.</param>
        public XMLImportTemplateReferenceException(string file) :
            base("Resolving a referenced file caused an exception." +
                $"\nFile: {file}")
        {
            File = file;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XMLImportTemplateReferenceException"/> class.
        /// </summary>
        /// <param name="file">The file that caused an exception.</param>
        /// <param name="innerException"><inheritdoc cref="Exception(string, Exception)" path="/param[@name='innerException']"/></param>
        public XMLImportTemplateReferenceException(string file, Exception innerException) :
            base("Resolving a referenced file caused an exception." +
                $"\nFile: {file}", innerException)
        {
            File = file;
        }
    }
}
