using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    /// <summary>
    /// Class which defines a voting registration object.
    /// </summary>
    public sealed class VotingRegistrationMeta
    {
        public string Key { get; set; }
        public string CorrelationId { get; set; }
        public  string Vote { get; set; }
        public string[] VoteOptions { get; set; }
        public int TotalMessages { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

}
