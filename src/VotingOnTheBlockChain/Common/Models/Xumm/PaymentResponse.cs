using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Xumm
{
 
    public class PaymentResponse
    {
        public string uuid { get; set; }
        public Next next { get; set; }
        public Refs refs { get; set; }
        public bool pushed { get; set; }
    }

    public class Next
    {
        public string always { get; set; }
    }

    public class Refs
    {
        public string qr_png { get; set; }
        public string qr_matrix { get; set; }
        public string[] qr_uri_quality_opts { get; set; }
        public string websocket_status { get; set; }
    }

}
