using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public class Enums
    {
        public enum Status
        {
            Ìnitial,
            processing,
            Success,
            Error
            
        }

        public enum RippledNetwork
        {
            Main,
            Test,
            Dev
        }

          public enum SynchronisationStatus
        {
            New,
            Processing,
            Complete,
            Error
        }

        public enum VotingStatus
        {
            Unknown,
            Eligible,
            Not_eligibe_no_active_trustline,
            Not_eligible_zero_balance,
        }

        public enum RunBookSteps
		{
            Load_configurations,
            Fetch_projeccts,
            Fetch_finalized_votings,
            Fetch_ongoing_votings,
            Fetch_cast_votes,
            Fetch_account_balances,
            Calculate_voting_results,
            Persist_voting_results

		}

        public enum RunBookStepStatus
        {
            Queued,
            Running,
            Completed,
            Failed,
            Aborted

        }

        public enum RunBookServerConnectionStatus
        {
            None = 0,
            Connecting = 1,
            Open = 2,
            CloseSent =3,
            ClsoeReceived = 4,
            Closed = 5,
            Aborted = 6

        }

        public enum AccountDetailsActionType
        {
            history,
            xrpscan,
            bithump,
            livenet
        }

        public enum OrderType
		{
            Undefined,
            Sell,
            Buy,
            Swap
		}

       
    }
}
