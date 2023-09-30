using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Xumm
{



    public class PayloadDetailsResponse
    {
        public Meta meta { get; set; }
        public Application application { get; set; }
        public Payload payload { get; set; }
        public Response response { get; set; }
        public Custom_Meta custom_meta { get; set; }
    }

    public class Meta
    {
        public bool exists { get; set; }
        public string uuid { get; set; }
        public bool multisign { get; set; }
        public bool submit { get; set; }
        public object pathfinding { get; set; }
        public string destination { get; set; }
        public string resolved_destination { get; set; }
        public bool resolved { get; set; }
        public bool signed { get; set; }
        public bool cancelled { get; set; }
        public bool expired { get; set; }
        public bool pushed { get; set; }
        public bool app_opened { get; set; }
        public bool opened_by_deeplink { get; set; }
        public object return_url_app { get; set; }
        public object return_url_web { get; set; }
        public bool is_xapp { get; set; }
        public object signers { get; set; }
    }

    public class Application
    {
        public string name { get; set; }
        public string description { get; set; }
        public int disabled { get; set; }
        public string uuidv4 { get; set; }
        public string icon_url { get; set; }
        public object issued_user_token { get; set; }
    }

    public class Payload
    {
        public string tx_type { get; set; }
        public string tx_destination { get; set; }
        public object tx_destination_tag { get; set; }
        public Request_Json request_json { get; set; }
        public string origintype { get; set; }
        public string signmethod { get; set; }
        public DateTime created_at { get; set; }
        public DateTime expires_at { get; set; }
        public int expires_in_seconds { get; set; }
    }

    public class Request_Json
    {
        public Options options { get; set; }
        public string TransactionType { get; set; }
        public bool SignIn { get; set; }
    }

    public class Options
    {
        public bool submit { get; set; }
        public int expire { get; set; }
        public Return_Url return_url { get; set; }
    }

    public class Return_Url
    {
        public string app { get; set; }
        public string web { get; set; }
    }

    public class Response
    {
        public string hex { get; set; }
        public string txid { get; set; }
        public DateTime resolved_at { get; set; }
        public string dispatched_to { get; set; }
        public string dispatched_nodetype { get; set; }
        public string dispatched_result { get; set; }
        public bool dispatched_to_node { get; set; }
        public string environment_nodeuri { get; set; }
        public string environment_nodetype { get; set; }
        public string multisign_account { get; set; }
        public string account { get; set; }
        public string signer { get; set; }
        public string user { get; set; }
    }

    public class Custom_Meta
    {
        public object identifier { get; set; }
        public object blob { get; set; }
        public object instruction { get; set; }
    }
}

