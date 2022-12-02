using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
    public class OrderBookProject
    {
        public string IssuerAccount { get; set; }
        public string IssuerName { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }
}
