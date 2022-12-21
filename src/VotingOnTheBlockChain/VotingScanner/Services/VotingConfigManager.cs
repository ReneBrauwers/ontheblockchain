using Azure.Storage.Blobs;
using Blazored.LocalStorage;
using Common.Models.Config;
using Common.Models.Report;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace VotingScanner.Services
{
    public sealed class VotingConfigManager
    {
        protected readonly IConfiguration _configuration;
        private List<ProjectConfig> _projectsConfig;
        private List<AccountWhitelist> _accountWhitelistSettings;
        private List<Voting> _votingRegistrationConfig;

        //public bool isProjectSettingConfigStale { get; private set; }
        //public bool isArchivedVotingConfigStale { get; private set; }
        //public bool isVotingsRegistrationConfigStale { get; private set; }
        //public bool isWhitelistConfigStale { get; private set; }
        public Dictionary<string, uint> lastArchivedVotingConfigIndex { get; private set; }

        private UriBuilder _uriLocation;

        public VotingConfigManager(IConfiguration configuration)
        {
            _configuration = configuration;         
            //isProjectSettingConfigStale = true;
            //isArchivedVotingConfigStale = true;
            //isVotingsRegistrationConfigStale = true;           
            //isWhitelistConfigStale = true;
            _uriLocation= new UriBuilder();
            _uriLocation.Scheme = "https";
            _uriLocation.Host = _configuration["RemoteConfigHostBlobStorage"];
            _uriLocation.Path = _configuration["ConfigFolderName"];


        }

        public async Task<List<ProjectConfig>> GetProjectsConfig()
        {

       
          _projectsConfig = new List<ProjectConfig>();
          _projectsConfig = await DownloadProjectConfigurationItems();


           
     
                foreach (var project in _projectsConfig)
                {
                    if (lastArchivedVotingConfigIndex is null)
                    {
                        lastArchivedVotingConfigIndex = new Dictionary<string, uint>();
                    }

                    lastArchivedVotingConfigIndex.Add(string.Concat(project.ProjectName, "-", project.ProjectToken), 0);
                }
              
            
            return _projectsConfig;

        }

        public async Task<VotingResultReport> GetReport(string reportName)
        {
           
            var report = new VotingResultReport();
            report = await DownloadVotingResults(reportName);
            return report;
        }
      

        public async Task<List<Voting>> GetVotingRegistrations()
        {
            //await GetProjectsConfig(); //get project config
            

          
                    _votingRegistrationConfig = new List<Voting>();
                    _votingRegistrationConfig = await DownloadVotingsRegistrationConfigurationItems();
           
          
            return _votingRegistrationConfig;

        }

        public async Task<List<AccountWhitelist>> GetAccountWhitelist()
        {

           
         
                _accountWhitelistSettings = new List<AccountWhitelist>();
                _accountWhitelistSettings = await DownloadAccountWhiteListConfigurationItems();
            return _accountWhitelistSettings;

        }


        private async Task<List<ProjectConfig>> DownloadProjectConfigurationItems(string fileName = "projectsconfig.json")
        {
            //Get projectsettings
            try
            {
                using (var client = new HttpClient())
                {                    
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", fileName);
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

        private async Task<List<AccountWhitelist>> DownloadAccountWhiteListConfigurationItems(string fileName = "accountwhitelist.json")
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/projectsettings.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(),"/", fileName);
                    var result = await client.GetFromJsonAsync<List<AccountWhitelist>>(downloadLink, CancellationToken.None);
                    return result;
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<VotingResultReport> DownloadVotingResults(string fileName)
        {
           
            try
            {
                using (var client = new HttpClient())
                {
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/", votingFileName);
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", fileName);
                    var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                    return result;
                }
            }
            catch
            {
                return null;
            }


        }
        private async Task<List<Voting>> DownloadVotingsRegistrationConfigurationItems(string fileName = "votingregistrations.json")
        {

            try
            {
                using (var client = new HttpClient())
                {
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/votingregistrations.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", fileName);
                    var result = await client.GetFromJsonAsync<List<Voting>>(downloadLink, CancellationToken.None);
                    return result;
                }
            }
            catch
            {
                return null;
            }

        }
     
        private async Task<List<VotingResultReport>> DownloadArchivedVotingResultItems(string fileNameNoExtension= "votingresult", int fileNumber = 1)
        {
            List<VotingResultReport> archivedVotingResultsReports = new List<VotingResultReport>();
            bool filesFound = true;
            while (filesFound)
            {
                
                try
                {
                    using (var client = new HttpClient())
                    {
                        //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/votingresult_", fileNumber, ".json");
                        var downloadLink = string.Concat(_uriLocation.ToString(), "/", fileNameNoExtension, "_", fileNumber, ".json");
                        var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                    }

                    fileNumber++;
                }
                catch
                {
                    filesFound = false;
                }

            }
            //Get projectsettings
            return archivedVotingResultsReports;

        }

  
       

    }
}
