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
using VotingScanner.Services;

namespace VotingResultsProcessor.Services
{
    public sealed class VotingReportManager
    {
        protected readonly IConfiguration _configuration;
        private VotingManager _votingManager;
        private QueueManager _queueManager;
        private PersistantStorageManager _persistantStorageManager;
        private Voting _voting;
        


        public VotingReportManager(IConfiguration configuration)
        {
            _configuration = configuration;
            _votingManager = new VotingManager(_configuration);
            _queueManager = new QueueManager(_configuration);
            _persistantStorageManager = new PersistantStorageManager(_configuration);
        }

        public async Task Start()
        {
            
            
            while(true)
            {
                if(await VotingProcessor())
                {                   
                    break;
                }
            }

            
        }

        private async Task<bool> VotingProcessor()
        {
           
            try
            {              
                    var votingInformation = await _queueManager.DeQueueMessage<Voting>(1, _configuration["STORAGE_ACCOUNT_QUEUES_SIGNATURE"]);
                    if (votingInformation != null)
                    {
                        Console.WriteLine($"processing {votingInformation.ProjectName} ${votingInformation.ProjectToken} - {votingInformation.VotingName}");
                        var votingReport = await CreateVotingReport(votingInformation);
                        var reportFileName = string.Concat(votingInformation.ProjectName, "/", votingInformation.ProjectToken, "/", votingInformation.VotingId, "-", votingInformation.VotingStartIndex, "-", votingInformation.VotingEndIndex, ".json");
                        await _persistantStorageManager.Upload<VotingResultReport>(_configuration["ConfigFolderName"], reportFileName, votingReport, _configuration["STORAGE_ACCOUNT_BLOBCONTAINER_SIGNATURE"]);
                    }
                    else
                    {
                    Console.WriteLine("No more voting data, exiting");
                        return true; // no more data, return true signalling to exit
                    }               


            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return true; //error occured, return true signalling to exit
            }

            return false; //not ready to exit as we have more data to process.
           
        }

        private async Task<VotingResultReport> CreateVotingReport(Voting voting)
        {
            var votingReport = new VotingResultReport();
            if (_voting is not null)
            {
                _voting = new Voting();
            }

            _voting = voting;

            //copy over project and voting information
            votingReport.ProjectToken = voting.ProjectToken;
            votingReport.ProjectName = voting.ProjectName;
            votingReport.VotingId = voting.VotingId;
            votingReport.VotingName = voting.VotingName;
            votingReport.LedgerStartIndex = voting.VotingStartIndex;
            votingReport.LedgerEndIndex = voting.VotingEndIndex;

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
            CancellationTokenSource ctx;// = new CancellationTokenSource(new TimeSpan(0, 20, 0)); // max timeout is 30 minutes
            var returnData = new List<AccountBalance>();
            //let's use paging; process 100 addresses at a time.
            var currentPage = 0;
            var takesPages = 100;
            var totalPages = Math.Ceiling(Convert.ToDecimal(addresses.Count() / Convert.ToDecimal(takesPages)));

            while (currentPage < totalPages)
            {
                Console.WriteLine($"data retrieval batch {currentPage+1} / {totalPages}");
                var itemsToSkip = currentPage * takesPages;
                var addressBatch = addresses.Skip(itemsToSkip).Take(takesPages).ToList();
                ctx = new CancellationTokenSource(new TimeSpan(0, 10, 0)); // max timeout is 10 minutes
                var intermediateResult = await _votingManager.GetVoterBalancesAsync(addressBatch, _voting.IssuerAccount, _voting.VotingEndIndex, _voting.ProjectToken, ctx, _configuration["websocketAddress"]);

                if(intermediateResult is not null && intermediateResult.Count > 0) 
                {
                    returnData.AddRange(intermediateResult);
                }

                currentPage++;
            }

            return returnData;

        }
    }
}
