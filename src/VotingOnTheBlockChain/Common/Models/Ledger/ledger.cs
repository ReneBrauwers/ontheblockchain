using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Ledger
{
    public class Ledger
    {
        public uint ledgerIndex { get; set; }
        public DateTime ledgerCloseTimeUTC { get; set; }
    }
}
