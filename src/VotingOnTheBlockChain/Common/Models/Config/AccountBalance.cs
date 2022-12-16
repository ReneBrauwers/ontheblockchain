using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    public class AccountBalance
    {
  
        /// <summary>
        /// Account which has casted the vote
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Choice of voter 
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Ledger index when vote was registered / received
        /// </summary>
        public string Currency { get; set; }

           /// <summary>
        /// Ledger Index when balance snapshot was taken
        /// </summary>
        public UInt32 LedgerIndex { get; set; }

  
    }
}
