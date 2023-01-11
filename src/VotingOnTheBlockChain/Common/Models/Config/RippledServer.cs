using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    public sealed class RippledServer
    {
        public string Network { get; set; } = "Main";
        public string Server { get; set; } = "wss://xrplcluster.com/";
    }
}
