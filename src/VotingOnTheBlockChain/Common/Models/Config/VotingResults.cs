using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    public class VotingResults
    {
        /// <summary>
        /// Unique Key, reserverd for future use; used to differentiate the organisers
        /// </summary>
        public string Key { get; set; } = Guid.Empty.ToString();

        /// <summary>
        /// Name of the vote
        /// </summary>
        public string Vote { get; set; }

        /// <summary>
        /// Unique vote Identifier
        /// </summary>
        public string VoteId { get; set; }

        /// <summary>
        /// Account which has casted the vote
        /// </summary>
        public string VoterAddress { get; set; }

        /// <summary>
        /// Choice of voter 
        /// </summary>
        public string VoterChoice { get; set; }

        /// <summary>
        /// Identifies the weight of votes (Ie 1 = 1 vote, 3000 = 3000 votes)
        /// </summary>
        public double VotingWeight { get; set; } = 0d;

        /// <summary>
        /// Ledger index when vote was registered / received
        /// </summary>
        public uint VoteRegistrationIndex { get; set; }

        /// <summary>
        /// UTC Data time representation when vote was registered / received
        /// </summary>
        public DateTime VoteRegistrationDateTime { get; set; }





    }
}
