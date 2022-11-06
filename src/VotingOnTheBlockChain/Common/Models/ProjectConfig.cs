using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{


    public class ProjectConfig
    {  
        public string ProjectName { get; set; }
      
        public string ProjectToken { get; set; }

        public string ProjectDescription { get; set; }

        public string ProjectImage { get; set; }

        public string ProjectLink { get; set; }

        public string IssuerAccount { get; set; }
        
        public string ControllerAccount { get; set; }
        
        public string VotingAccount { get; set; }

        public uint LedgerVotingStartIndex { get; set; }
     
    }
    
}
