using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Models.Account
{
    public class AccountOffers
    {
        public OrderType Side { get; set; }
        public string Account { get; set; }
        public string AccountAlias { get; set; }

        //new entries
        public OrderType TypeOfOrder { get; set; }
        public string InCurrency { get; set; }
        public decimal InAmount { get; set; }

        public string OutCurrency { get; set; }
        public decimal OutAmount { get; set; }

        public string ExchangeRate { get; set; }
        public decimal ExchangeRateVal { get; set; }

     
    }


}
