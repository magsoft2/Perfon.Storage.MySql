using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perfon.Interfaces.Common;

namespace Perfon.Storage.MySql
{
    /// <summary>
    /// Used for reporting errors occured inside storage
    /// </summary>
    public class PerfonErrorEventArgs : EventArgs, IPerfonErrorEventArgs
    {
        /// <summary>
        /// Description of the error
        /// </summary>
        public string Message { get; set; }

        public PerfonErrorEventArgs(string message)
        {
            Message = message;
        }
    }
    
}
