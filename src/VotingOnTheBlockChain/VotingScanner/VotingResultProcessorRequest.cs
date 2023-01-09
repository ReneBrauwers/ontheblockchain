using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VotingScanner
{
    public sealed class VotingResultProcessorRequest
    {
        public int instances { get; set; } = 1;
        public string location { get; set; } = "westus2";
    }
}
