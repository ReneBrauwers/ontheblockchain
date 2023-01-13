using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Models.Config
{
    public sealed class RippledServer
    {
        public RippledNetwork Network { get; set; } = RippledNetwork.Main;
        public string Server { get; set; } = "wss://xrplcluster.com/";
        public bool IsConnected { get; set; } = false;
    }
}
