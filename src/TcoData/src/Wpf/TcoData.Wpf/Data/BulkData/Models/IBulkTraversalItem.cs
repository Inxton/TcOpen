
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TcoData.Models;

namespace TcoData.Models
{
    public interface IBulkTraversalItem 
    {
        string Symbol { get; set; }
        object Value { get; set; }
        BulkItemStatus Status { get; set; }
        BulkItemWriteStatus WriteStatus { get; set; }
    }
}
