using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Models.Account
{
    public class AccountOrderBook
    {
        public OrderType Side { get; set; }
        public string Account { get; set; }
        public string AccountAlias { get; set; }
        public string Currency { get; set; }
        public string Issuer { get; set; }
        public double Price { get; set; }
        public double Volume { get; set; }
        public double Total { get; set; }
        public string OrderSummary { get; set; }
    }
}
