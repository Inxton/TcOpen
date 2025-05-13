using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcoData.Models
{
    public enum BulkItemWriteStatus
    {
        NoChange = 0,
        Modified = 10,
 
    }
    public static class BulkItemWriteStatuses
    {
        public static Array All => Enum.GetValues(typeof(BulkItemWriteStatus));
    }
}
