using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace VotingCommission.Models
{

    public sealed class VotingAnnouncement
    {
        public Guid Uid { get; set; }
        public string Organisation { get; set; }
        public string VotingAccount { get; set; }
        public string IssuerAccount { get; set; }
        public string ControllerAccount { get; set; }
        public string Model { get; set; } = "Standard";
        public string Topic { get; set; }
        public string Choices { get; set; }
        public float VotingFeeAmount { get; set; } = 0;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool TrustlineRequired { get; set; } = false;
        public string TokenName { get; set; }
    }
}
