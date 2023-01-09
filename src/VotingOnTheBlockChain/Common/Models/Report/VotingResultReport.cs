using Common.Models.Config;

namespace Common.Models.Report
{
    public class VotingResultReport
    {
        public string ProjectToken { get; set; }
        public string ProjectName { get; set; }
        public string VotingId { get; set; }
        public string VotingName { get; set; }
        public uint LedgerStartIndex { get; set; }
        public uint LedgerEndIndex { get; set; }
        public decimal TotalVotesCast { get; set; }
        public int TotalAccountsVoted { get; set; }
        public int UniqueAccountsVoted { get; set; }
        public int UniqueValidAccountsVoted { get; set; }
        public int totalInvalidatedVotes { get; set; }
        public List<VoteResults> Details { get; set; }

    }
    public class VoteResults
    {        
        public string Option { get; set; }
        public decimal TotalVotes { get; set; }
        public int TotalValidAccountsVoted { get; set; }
        public decimal PercentageVote { get; set; }
        public decimal PercentagePopularVote { get; set; }
        public List<AccountBalance> votingAccountDetails { get; set; }
    }

}
