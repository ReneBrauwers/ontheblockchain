using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Models.Account
{
    public class AccountInformation
    {
        public string? Account { get; set; }
        public string? Alias { get; set; }
        public string? LookupLink { get; set; }
        public bool? IsWhitelisted { get; set; }
        public decimal? Balance { get; set; }
    }
      
}
