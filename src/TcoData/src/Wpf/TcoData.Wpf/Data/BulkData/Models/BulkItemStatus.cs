using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcoData.Models
{
    public enum BulkItemStatus
    {
        All = -1,
        Undefined = 0,
        Editable = 10,
        Deleted = 20,
    }
    public static class BulkItemStatuses
    {
        public static Array All => Enum.GetValues(typeof(BulkItemStatus));
    }
}
