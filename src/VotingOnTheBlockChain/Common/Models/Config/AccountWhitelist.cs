using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Config
{
	public class AccountWhitelist
	{
		public string Address { get; set; } = string.Empty;
        public string FriendlyAddress { get; set; } = string.Empty;
		public string Link { get; set; } = string.Empty;
		public bool isConfirmed { get; set; }

	}

 
}
