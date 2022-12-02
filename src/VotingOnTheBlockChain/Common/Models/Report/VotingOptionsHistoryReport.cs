using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Report
{
    public class VotingOptionsHistoryReport
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string VotingId { get; set; }
        public string VotingName { get; set; }
        public string VotingOption { get; set; }
        public DateTime VotingEndDate { get; set; }
        public string AccountId { get; set; }
        public double AccountBalance { get; set; }
        public int LedgerIndex { get; set; }
    }
}
