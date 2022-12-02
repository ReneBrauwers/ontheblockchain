using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    public class VotingRegistrations
    {
        public string ProjectToken { get; set; }
        public string ProjectName { get; set; }
        public string VotingId { get; set; }
        public string VotingName { get; set; }
        public string VotingAccount { get; set; }
        public string VotingControllerAccount { get; set; }
        public uint VotingStartIndex { get; set; }
        public uint VotingEndIndex { get; set; }
        public string VotingDataReference { get; set; }
        public bool IsLive { get; set; }
        public string[] VotingOptions { get; set; }
    }
}
