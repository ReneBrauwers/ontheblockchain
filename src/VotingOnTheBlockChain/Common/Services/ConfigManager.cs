using Blazored.LocalStorage;
using Common.Extensions;
using Common.Handlers;
using Common.Models.Config;
using Common.Models.Report;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System.Data.SqlTypes;
using System.Net.Http.Json;
using System.Xml;
using static Common.Extensions.Enums;

namespace Common.Services
{
    public sealed class ConfigManager
    {
        protected readonly IConfiguration _configuration;        
        private List<ProjectConfig> _projectsConfig;
        private List<OrderBookProject> _orderBookProjectSettings;
        private List<AccountWhitelist> _accountWhitelistSettings;
        private List<Voting> _votingRegistrationConfig;      
        

        private UriBuilder _uriLocation;
        private RippledNetwork _ActiveNetwork { get; set; } = RippledNetwork.Main;

        public ConfigManager(NavigationManager navManager, IConfiguration configuration)
        {
            _configuration = configuration;            
           
            //set up location uri, used for downloading the config files          
            _uriLocation = new UriBuilder();
            _uriLocation.Scheme = "https";
            _uriLocation.Host = _configuration["RemoteConfigHost"];          

            SetActiveNetwork(_ActiveNetwork);          


        }

        public void SetActiveNetwork(RippledNetwork rippledServerState)
        {
            _ActiveNetwork = rippledServerState;
            _uriLocation.Path = Path.Combine(_configuration["ConfigFolderName"],_ActiveNetwork.ToString());

        }
        public async Task<List<ProjectConfig>> GetProjectsConfig()
        {
            _projectsConfig = new List<ProjectConfig>();
            _projectsConfig = await DownloadProjectConfigurationItems();
            return _projectsConfig;
        }

      
        public async Task<List<OrderBookProject>> GetOrderBookProjectSettings()
        {
            _orderBookProjectSettings = new List<OrderBookProject>();
            _orderBookProjectSettings = await DownloadOrderBookProjectConfigurationItems();
            return _orderBookProjectSettings;
        }

     
        public async Task<List<AccountWhitelist>> GetAccountWhitelist()
        {
  
           _accountWhitelistSettings = new List<AccountWhitelist>();
            _accountWhitelistSettings = await DownloadAccountWhiteListConfigurationItems();
            return _accountWhitelistSettings;

        }

    
        private async Task<List<ProjectConfig>> DownloadProjectConfigurationItems()
        {
            //Get projectsettings
            try
            {
                using (var client = new HttpClient())
                {
                    
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/projectsconfig.json");
                    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                    var result = await client.GetFromJsonAsync<List<ProjectConfig>>(downloadLink, CancellationToken.None);                    
                    return result;
                }
            }
            catch
            {
                return null;
            }





        }

        private async Task<List<AccountWhitelist>> DownloadAccountWhiteListConfigurationItems()
        {
            try
            {
                using (var client = new HttpClient())
                {
                   
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/accountwhitelist.json");
                    var result = await client.GetFromJsonAsync<List<AccountWhitelist>>(downloadLink, CancellationToken.None);                   
                    return result;
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<VotingResultReport> DownloadVotingResults(string votingFileName)
        {
            //Get projectsettings
            try
            {
                using (var client = new HttpClient())
                {
                    
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", votingFileName);
                    var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                    
                    return result;
                }
            }
            catch
            {
                return null;
            }





        }
        private async Task<List<Voting>> DownloadVotingsRegistrationConfigurationItems()
        {

            try
            {
                using (var client = new HttpClient())
                {
                    
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/votingregistrations.json");
                    var result = await client.GetFromJsonAsync<List<Voting>>(downloadLink, CancellationToken.None);

                    return result;
                }
            }
            catch
            {
                return null;
            }

        }
        private async Task<List<OrderBookProject>> DownloadOrderBookProjectConfigurationItems()
        {
            //Get orderbookprojectsettings
            try
            {
                using (var client = new HttpClient())
                {
                   
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/orderbooksconfig.json");
                    var result = await client.GetFromJsonAsync<List<OrderBookProject>>(downloadLink, CancellationToken.None);

                    return result;
                }
            }
            catch
            {
                return null;
            }





        }



        public async Task<VotingResultReport> DownloadArchivedVotingResultItem(string projectName, string projectToken, string votingId, uint startledgerindex, uint endLedgerIndex)
        {
          
            try
            {
                using (var client = new HttpClient())
                {
                    
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", projectName, "/", projectToken, "/", votingId, "-", startledgerindex, "-", endLedgerIndex, ".json");
                    var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);

                    return result;
                }


            }
            catch
            {
                return null;
            }



        }
        public async Task<List<Voting>> GetVotingResultManifest()
        {
            //Get projectsettings
            List<Voting> manifest = new List<Voting>();
            try
            {
                using (var client = new HttpClient())
                {
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/", votingFileName);
                    var downloadLink = string.Concat(_uriLocation.ToString().Replace(_ActiveNetwork.ToString(),string.Empty), "?restype=container&comp=list");
                    var result = await client.GetStringAsync(downloadLink, CancellationToken.None);// GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(result);
                        XmlNodeList urlNodeList = xmlDoc.SelectNodes("//Url");
                        foreach (XmlNode item in urlNodeList)
                        {
                            var configurationItemPathArray = item.InnerText.Split(new string[] { string.Concat(_uriLocation.ToString(), "/") }, StringSplitOptions.RemoveEmptyEntries);
                            if (configurationItemPathArray != null && configurationItemPathArray.Length == 1)
                            {
                                //split on /
                                var votingManifestEntryRaw = configurationItemPathArray[0].Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                                if (votingManifestEntryRaw.Length == 3)
                                {
                                    var votingResultManifest = new Voting();
                                    votingResultManifest.ProjectName = votingManifestEntryRaw[0];
                                    votingResultManifest.ProjectToken = votingManifestEntryRaw[1];
                                    votingResultManifest.VotingResultFile = item.InnerText;
                                    votingResultManifest.IsLive = false;
                                    var votingMetaData = votingManifestEntryRaw[2].Replace(".json", string.Empty).Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries);
                                    if (votingMetaData.Length > 2)
                                    {
                                        votingResultManifest.VotingId = votingMetaData[0];
                                        votingResultManifest.VotingName = votingMetaData[0].HexToString();
                                        votingResultManifest.VotingStartIndex = Convert.ToUInt32(votingMetaData[1]);
                                        votingResultManifest.VotingEndIndex = Convert.ToUInt32(votingMetaData[2]);
                                    }
                                    else
                                    {
                                        votingResultManifest.VotingId = votingMetaData[0];
                                        votingResultManifest.VotingName = votingMetaData[0].HexToString();
                                        votingResultManifest.VotingStartIndex = Convert.ToUInt32(votingMetaData[1].Replace(".json", string.Empty));
                                        votingResultManifest.VotingEndIndex = 0;
                                    }

                                    manifest.Add(votingResultManifest);
                                }

                            }

                        }
                    }


                }
            }
            catch
            {
                return default;
            }

            return manifest.OrderBy(x => x.ProjectName).ThenBy(x => x.ProjectToken).ThenByDescending(x => x.VotingStartIndex).ToList();
        }

    }
}
