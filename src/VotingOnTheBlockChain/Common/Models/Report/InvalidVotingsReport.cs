using Common.Models.Config;

namespace Common.Models.Report
{
    public class InvalidVotingsReport
    {
        public string Address { get; set; }
        public uint LedgerIndex { get; set; }
        public DateTime? VoteRecordedDateTime { get; set; }
        public string Option { get; set; }
        public decimal TotalVotes { get; set; }
        public string Reason { get; set; }
      
    }

}
