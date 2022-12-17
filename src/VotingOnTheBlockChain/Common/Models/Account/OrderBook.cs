using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Models.Account
{
    public class OrderBook
    {
        public OrderType Side { get; set; }
        public string Account { get; set; }
        public string AccountAlias { get; set; }
        public string Currency { get; set; }
        public string Issuer { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public decimal Total { get; set; }
        public string OrderSummary { get; set; }
    }
}
