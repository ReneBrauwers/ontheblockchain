using Common.Models.Config;
using Common.Models.Report;
using Common.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VotingResultsProcessor.Services
{
    public sealed class VotingReportManager
    {
        protected readonly IConfiguration _configuration;
        private VotingManager _votingManager;
        private Voting _voting;


        public VotingReportManager(IConfiguration configuration)
        {
            _configuration = configuration;
            _votingManager = new VotingManager(_configuration);
        }

        public async Task<VotingResultReport> CreateVotingReport(Voting voting)
        {
            var votingReport = new VotingResultReport();
            if (_voting is not null)
            {
                _voting = new Voting();
            }

            _voting = voting;

            //add voting options to report
            if (_voting?.VotingOptions?.Count() > 0)
            {
                votingReport.Details = new();

                foreach (var voteOption in _voting.VotingOptions)
                {
                    votingReport.Details.Add(new VoteResults()
                    {
                        Option = voteOption,
                        votingAccountDetails = new()

                    });
                }

            }

            #region popular votes
            List<VotingResults> validVotingResults = new();
           
            //get popular votes
            var allVotes = await GetPopularVotesFromXRPL();

            //retain only last votes
            foreach(var multipleVoters in allVotes.GroupBy(x => x.VoterAddress).Where(g => g.Count() > 1))
            {
                var lastVote = multipleVoters.OrderByDescending(x => x.VoteRegistrationIndex).FirstOrDefault();
                validVotingResults.Add(lastVote);
            }

            //copy over the single voters
            foreach (var singleVoters in allVotes.GroupBy(x => x.VoterAddress).Where(g => g.Count() == 1))
            {                 
                validVotingResults.AddRange(singleVoters);
            }                    

            //reset
            allVotes = null;

            #endregion

            #region get account balances
            //get account balances

            List<InvalidVotingsReport> invalidVotingsReport = new List<InvalidVotingsReport>(); //placeholder which contains the invalidated votes
            int accountsWithVotes = 0;
            var accountBalances = await GetAccountBalanceFromXRPL(validVotingResults.Select(x => x.VoterAddress).ToList());
             
            foreach (var result in accountBalances)
            {
                var workingPopularVote = validVotingResults.Where(x => x.VoterAddress == result.Address).OrderByDescending(x => x.VoteRegistrationIndex).FirstOrDefault();

                //now update the last know voting date/time (should allign with the ledger index)
                result.LastRecordedVoteDateTime = workingPopularVote?.VoteRegistrationDateTime.ToLocalTime();


                //add item to vote
                var selectedOption = votingReport.Details.FirstOrDefault(x => x.Option == workingPopularVote?.VoterChoice);
                if (selectedOption is not null)
                {
                    selectedOption.votingAccountDetails.Add(result);
                    accountsWithVotes++; //actual valid accounts as they have a vote count > 0
                }
                else
                {
                    //find voter and make vote invalid
                    result.IsValid= false;
                    result.InvalidReason = $"{workingPopularVote?.VoterChoice} not part of valid vote options";
                    
                }

            }
            #endregion

            #region Summarize
            var totalVotesCast = votingReport.Details.SelectMany(x => x.votingAccountDetails).Sum(x => x.Balance);
            votingReport.TotalVotesCast = votingReport.Details.SelectMany(x => x.votingAccountDetails).Sum(x => x.Balance);
            votingReport.UniqueValidAccountsVoted = votingReport.Details.SelectMany(x => x.votingAccountDetails).Count(x => x.IsValid);
            votingReport.totalInvalidatedVotes = votingReport.Details.SelectMany(x => x.votingAccountDetails).Count(x => (!x.IsValid));           
            votingReport.UniqueAccountsVoted = validVotingResults.Select(x => x.VoterAddress).Distinct().Count();
            votingReport.TotalAccountsVoted = validVotingResults.Select(x => x.VoterAddress).Count();
            

            foreach (var detailItem in votingReport.Details)
            {
                detailItem.TotalVotes = detailItem.votingAccountDetails.Sum(x => x.Balance);
                detailItem.TotalValidAccountsVoted = detailItem.votingAccountDetails.Count;
                if (votingReport.TotalVotesCast > 0)
                {
                    detailItem.PercentageVote = (detailItem.TotalVotes * 100) / votingReport.TotalVotesCast;
                }

            }

            #endregion

            return votingReport;
          
        }


        private async Task<List<VotingResults>> GetPopularVotesFromXRPL()
        {
            CancellationTokenSource ctx = new CancellationTokenSource(new TimeSpan(0, 20, 0)); // max timeout is 20 minutes
            return await _votingManager.GetVotingResultsAsync(_voting.VotingAccount, _voting.VotingControllerAccount, _voting.VotingId, _voting.VotingStartIndex, _voting.VotingEndIndex, ctx, _configuration["websocketAddress"]);

        }

        private async Task<List<AccountBalance>> GetAccountBalanceFromXRPL(List<string> addresses)
        {
            CancellationTokenSource ctx = new CancellationTokenSource(new TimeSpan(4, 0, 0)); // max timeout is 4 hr
            return await _votingManager.GetVoterBalancesAsync(addresses, _voting.IssuerAccount, _voting.VotingEndIndex, _voting.ProjectToken, ctx, _configuration["websocketAddress"]);

        }
    }
}
