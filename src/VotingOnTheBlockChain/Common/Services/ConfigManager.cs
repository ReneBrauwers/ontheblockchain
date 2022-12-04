using Blazored.LocalStorage;
using Common.Models.Config;
using Common.Models.Report;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Common.Services
{
    public class ConfigManager
    {
        protected readonly IConfiguration _configuration;
        protected readonly ILocalStorageService _localStorage;
        private List<ProjectConfig> _projectsConfig;
        private List<OrderBookProject> _orderBookProjectSettings;
        private List<AccountWhitelist> _accountWhitelistSettings;
        private List<VotingRegistrations> _votingRegistrationConfig;

        public bool isProjectSettingConfigStale { get; private set; }
        public bool isArchivedVotingConfigStale { get; private set; }
        public bool isVotingsRegistrationConfigStale { get; private set; }
        public bool isOrderBookOrderConfigStale { get; private set; }

        public bool isWhitelistConfigStale { get; private set; }
        public Dictionary<string, uint> lastArchivedVotingConfigIndex { get; private set; }

        private UriBuilder _uriLocation;

        public ConfigManager(NavigationManager navManager, IConfiguration configuration, ILocalStorageService localStorage)
        {
            _configuration = configuration;
            _localStorage = localStorage;
            isProjectSettingConfigStale = true;
            isArchivedVotingConfigStale = true;
            isVotingsRegistrationConfigStale = true;
            isOrderBookOrderConfigStale = true;
            isWhitelistConfigStale = true;

            //set up location uri, used for downloading the config files
            bool isLocalConfig = true;
            _uriLocation = new UriBuilder();
            if (bool.TryParse(_configuration["IsLocalConfigFolder"], out isLocalConfig))
            {

                if (isLocalConfig)
                {
                    _uriLocation.Scheme = "https";
                    _uriLocation.Host = new Uri(navManager.BaseUri).DnsSafeHost;
                    _uriLocation.Port = navManager.ToAbsoluteUri(_uriLocation.Host).Port;

                }
                else
                {
                    _uriLocation.Scheme = "https";
                    _uriLocation.Host = _configuration["RemoteConfigHost"];

                }
            }
            _uriLocation.Path = _configuration["ConfigFolderName"];


        }

        public async Task<string> GetProjectVersion()
        {
            return await DownloadApplicationVersion();

        }
        public async Task<List<ProjectConfig>> GetProjectsConfig()
        {

            bool loadWithDefaultValues = false;
            if (isProjectSettingConfigStale || _projectsConfig is null)
            {
                try
                {
                    _projectsConfig = await _localStorage.GetItemAsync<List<ProjectConfig>>("projectsConfig", CancellationToken.None);
                    if (_projectsConfig is null)
                    {

                        loadWithDefaultValues = true;
                    }

                }
                catch
                {

                    await _localStorage.RemoveItemAsync("projectsConfig");
                    loadWithDefaultValues = true;
                }
            }

            if (loadWithDefaultValues)
            {
                _projectsConfig = new List<ProjectConfig>();
                _projectsConfig = await DownloadProjectConfigurationItems();


            }
            if (isProjectSettingConfigStale)
            {
                foreach (var project in _projectsConfig)
                {
                    if (lastArchivedVotingConfigIndex is null || isArchivedVotingConfigStale)
                    {
                        lastArchivedVotingConfigIndex = new Dictionary<string, uint>();
                    }
                   
                    lastArchivedVotingConfigIndex.Add(String.Concat(project.ProjectName, "-", project.ProjectToken), 0);
                }
                isProjectSettingConfigStale = false;
            }
            return _projectsConfig;

        }

        public async Task<VotingResultReport> GetReport(string reportName)
        {
            bool loadWithDefaultValues = false;
            var report = new VotingResultReport();

            try
            {
                //check local storage
                report = await _localStorage.GetItemAsync<VotingResultReport>(reportName, CancellationToken.None);

                if (report is null) // not null)
                {
                    loadWithDefaultValues = true;
                }

            }
            catch
            {
                await _localStorage.RemoveItemAsync(reportName);
                loadWithDefaultValues = true;
            }

            if (loadWithDefaultValues)
            {

                report = await DownloadVotingResults(reportName);


            }


            return report;
        }
        public async Task<List<OrderBookProject>> GetOrderBookProjectSettings()
        {
            bool loadWithDefaultValues = false;
            if (isOrderBookOrderConfigStale || _orderBookProjectSettings is null)
            {
                try
                {
                    _orderBookProjectSettings = await _localStorage.GetItemAsync<List<OrderBookProject>>("orderBookProjectSettings", CancellationToken.None);
                    if (_orderBookProjectSettings is null) // not null)
                    {
                        //isProjectSettingConfigStale = false;
                        loadWithDefaultValues = true;
                    }
                    //else
                    //{
                    //    loadWithDefaultValues = true;
                    //}
                }
                catch
                {

                    await _localStorage.RemoveItemAsync("orderBookProjectSettings");
                    loadWithDefaultValues = true;
                }
            }

            if (loadWithDefaultValues)
            {
                _orderBookProjectSettings = new List<OrderBookProject>();
                _orderBookProjectSettings = await DownloadOrderBookProjectConfigurationItems();


            }


            isOrderBookOrderConfigStale = false;

            return _orderBookProjectSettings;

        }

        public async Task<List<VotingRegistrations>> GetVotingRegistrations()
        {
            bool loadWithDefaultValues = false;

            if (isProjectSettingConfigStale)
            {
                await GetProjectsConfig(); //get project config
            }

            if (_votingRegistrationConfig is null || isVotingsRegistrationConfigStale)
            {

                try
                {
                    //check local storage
                    _votingRegistrationConfig = await _localStorage.GetItemAsync<List<VotingRegistrations>>("votingregistrations", CancellationToken.None);


                    if (_votingRegistrationConfig is null) // not null)
                    {

                        loadWithDefaultValues = true;
                    }

                }
                catch
                {
                    await _localStorage.RemoveItemAsync("votingregistrations");
                    loadWithDefaultValues = true;
                }

                if (loadWithDefaultValues)
                {
                    _votingRegistrationConfig = new List<VotingRegistrations>();
                    _votingRegistrationConfig = await DownloadVotingsRegistrationConfigurationItems();


                }


            }

            isVotingsRegistrationConfigStale = false;
            return _votingRegistrationConfig;

        }

        public async Task<List<AccountWhitelist>> GetAccountWhitelist()
        {

            bool loadWithDefaultValues = false;
            if (isWhitelistConfigStale || _accountWhitelistSettings is null)
            {
                try
                {
                    _accountWhitelistSettings = await _localStorage.GetItemAsync<List<AccountWhitelist>>("accountwhitelist", CancellationToken.None);
                    if (_accountWhitelistSettings is null)
                    {

                        loadWithDefaultValues = true;
                    }

                }
                catch
                {

                    await _localStorage.RemoveItemAsync("accountwhitelist");
                    loadWithDefaultValues = true;
                }
            }

            if (loadWithDefaultValues)
            {
                _accountWhitelistSettings = new List<AccountWhitelist>();
                _accountWhitelistSettings = await DownloadAccountWhiteListConfigurationItems();


            }

            return _accountWhitelistSettings;

        }



        public async Task ClearLocalStore()
        {
            foreach (var key in await _localStorage.KeysAsync())
            {
                await _localStorage.RemoveItemAsync(key);
                isProjectSettingConfigStale = true;
                isArchivedVotingConfigStale = true;
                isOrderBookOrderConfigStale = true;
            }
        }
        private async Task<List<ProjectConfig>> DownloadProjectConfigurationItems()
        {
            //Get projectsettings
            try
            {
                using (var client = new HttpClient())
                {
                    // var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/projectsettings.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/projectsconfig.json");
                    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                    var result = await client.GetFromJsonAsync<List<ProjectConfig>>(downloadLink, CancellationToken.None);
                    if (result is not null)
                    {
                        await _localStorage.SetItemAsync<List<ProjectConfig>>("projectsConfig", result, CancellationToken.None);
                    }

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
                    // var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/projectsettings.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/accountwhitelist.json");
                    var result = await client.GetFromJsonAsync<List<AccountWhitelist>>(downloadLink, CancellationToken.None);
                    if (result is not null)
                    {
                        await _localStorage.SetItemAsync<List<AccountWhitelist>>("accountwhitelist", result, CancellationToken.None);
                    }

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
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/", votingFileName);
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/", votingFileName);
                    var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                    if (result is not null)
                    {
                        await _localStorage.SetItemAsync<VotingResultReport>(votingFileName, result, CancellationToken.None);
                    }

                    return result;
                }
            }
            catch
            {
                return null;
            }





        }
        private async Task<List<VotingRegistrations>> DownloadVotingsRegistrationConfigurationItems()
        {

            try
            {
                using (var client = new HttpClient())
                {
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/votingregistrations.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/votingregistrations.json");
                    var result = await client.GetFromJsonAsync<List<VotingRegistrations>>(downloadLink, CancellationToken.None);
                    if (result is not null)
                    {
                        await _localStorage.SetItemAsync<List<VotingRegistrations>>("votingregistrations", result, CancellationToken.None);
                    }

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
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/orderbooksettings.json");
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/orderbooksettings.json");
                    var result = await client.GetFromJsonAsync<List<OrderBookProject>>(downloadLink, CancellationToken.None);
                    if (result is not null)
                    {
                        await _localStorage.SetItemAsync<List<OrderBookProject>>("orderBookProjectSettings", result, CancellationToken.None);
                    }

                    return result;
                }
            }
            catch
            {
                return null;
            }





        }
        private async Task<List<VotingResultReport>> DownloadArchivedVotingResultItems(int fileNumber = 1)
        {
            List<VotingResultReport> archivedVotingResultsReports = new List<VotingResultReport>();
            bool filesFound = true;
            while (filesFound)
            {
                VotingResultReport archivedVotingResult = new VotingResultReport();

                try
                {
                    using (var client = new HttpClient())
                    {
                        //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/votingresult_", fileNumber, ".json");
                        var downloadLink = string.Concat(_uriLocation.ToString(), "/votingresult_", fileNumber, ".json");
                        var result = await client.GetFromJsonAsync<VotingResultReport>(downloadLink, CancellationToken.None);
                        if (result is not null)
                        {
                            archivedVotingResultsReports.Add(result);
                            await _localStorage.SetItemAsync<VotingResultReport>(string.Concat("votingresult_", fileNumber), result, CancellationToken.None);
                        }
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

        private async Task<string> DownloadApplicationVersion()
        {
            //Get projectsettings
            try
            {
                using (var client = new HttpClient())
                {
                    //var downloadLink = string.Concat(_configuration["PublicConfigRepoUri"], "/version.txt", "?dt=", DateTime.Now.Ticks);
                    var downloadLink = string.Concat(_uriLocation.ToString(), "/version.txt", "?dt=", DateTime.Now.Ticks);
                    var result = await client.GetStringAsync(downloadLink, CancellationToken.None);
                    return result;
                }
            }
            catch
            {
                return String.Empty;
            }





        }

    }
}
