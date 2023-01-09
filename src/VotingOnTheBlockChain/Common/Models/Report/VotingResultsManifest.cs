using Common.Models.Config;

namespace Common.Models.Report
{
    public class VotingResultManifest
    {
        public string ProjectToken { get; set; }
        public string ProjectName { get; set; }
        public string VotingId { get; set; }
        public string VotingName { get; set; }
        public uint LedgerStartIndex { get; set; }
        public uint LedgerEndIndex { get; set; }
        public Uri VotingResultFile { get; set; }

    }
 

}
