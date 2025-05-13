using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcoData.Models
{
    public enum BulkItemWriteStatus
    {
        /// <summary>
        /// The item has not been modified or queued for writing.
        /// </summary>
        NoChange = 0,

        /// <summary>
        /// The item has been edited and is pending a write operation.
        /// </summary>
        Modified = 10

    }
    public static class BulkItemWriteStatuses
    {
        public static Array All => Enum.GetValues(typeof(BulkItemWriteStatus));
    }
}
