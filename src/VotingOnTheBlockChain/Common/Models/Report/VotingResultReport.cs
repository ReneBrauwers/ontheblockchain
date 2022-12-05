using Common.Models.Config;

namespace Common.Models.Report
{
    public class VotingResultReport
    {
        public string ProjectToken { get; set; }
        public string ProjectName { get; set; }
        public string VotingId { get; set; }
        public string VotingName { get; set; }
        public uint LedgerIndex { get; set; }
        public double TotalVotesCast { get; set; }
        public int UniqueAccountsVoted { get; set; }
        public List<VoteResults> Details { get; set; }
    }
    public class VoteResults
    {        
        public string Option { get; set; }
        public double TotalVotes { get; set; }
        public int TotalAccountsVotedFor { get; set; }
        public double PercentageVote { get; set; }
        public List<AccountBalance> votingAccountDetails { get; set; }
    }

}
