using Common.Models.Config;
using Common.Models.Report;
using Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using VotingScanner;
using VotingScanner.Services;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();


//obtain storage account key from environment variable
var storageAccountBlobContainerKey = config["STORAGE_ACCOUNT_BLOBCONTAINER_SIGNATURE"];
var storageAccountQueueKey = config["STORAGE_ACCOUNT_QUEUES_SIGNATURE"];
var rippledServer = config["websocketAddress"];

//DI
var services = new ServiceCollection();
services.AddSingleton(x => new VotingConfigManager(config));
services.AddSingleton(x => new VotingManager(config));
services.AddSingleton(x => new PersistantStorageManager(config));
services.AddSingleton(x => new QueueManager(config));
//services.AddTransient<sample>(x => new sample(config));
var sp = services.BuildServiceProvider();

var _configManager = sp.GetRequiredService<VotingConfigManager>();
var _votingManager = sp.GetRequiredService<VotingManager>();
var _persistantStorageManager = sp.GetRequiredService<PersistantStorageManager>();
var _queueManager = sp.GetRequiredService<QueueManager>();
//var synchronisation = sp.GetRequiredService<votingsynchronisation>();
//var streaming = sp.GetRequiredService<sample>();


/* steps of execution
 * 1) Retrieve settings for all projects
 * 2) Determine votings to sync
 * 3) Extract information for votings which need to be retrieved and publish them on a queue; for processing
 */
Console.WriteLine("Retrieve settings for all projects");
var projectConfigurations = await _configManager.GetProjectsConfig();

//Retrieve data on already stored votings
Console.WriteLine("Retrieve data on already stored votings");

//var storedVotingInformation = new List<Voting>();
var storedVotingInformation = await _configManager.GetVotingRegistrations();


//Get last indexes per project
Dictionary<string, uint> lastRecordedVotingIndexesByProjectToken = new Dictionary<string, uint>();
if (storedVotingInformation is not null && storedVotingInformation.Count > 0)
{
    foreach (var items in storedVotingInformation.GroupBy(x => x.ProjectName))
    {
        var projectName = items.Key;
        foreach (var item in items.GroupBy(x => x.ProjectToken))
        {
            var tokenName = item.Key;
            //get max ledger index
            var maxVotingIndex = item.Max(x => x.VotingEndIndex);

            var compositeKey = string.Concat(projectName, "-", tokenName);

            if (lastRecordedVotingIndexesByProjectToken.ContainsKey(compositeKey))
            {
                lastRecordedVotingIndexesByProjectToken[compositeKey] = maxVotingIndex;
            }
            else
            {
                lastRecordedVotingIndexesByProjectToken.Add(compositeKey, maxVotingIndex);
            }

        }
    }
}
else
{
    storedVotingInformation = new List<Voting>();
}

List<ProjectConfig> updatedProjectConfigurations = new List<ProjectConfig>();
if (lastRecordedVotingIndexesByProjectToken.Count > 0)
{
    //only retrieve data from last known point
    foreach (var item in lastRecordedVotingIndexesByProjectToken)
    {
        var projectNameAndToken = item.Key.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries);
        var projectName = projectNameAndToken[0];
        var projectToken = projectNameAndToken[1];
        var existingProjectInfo = projectConfigurations.Where(x => x.ProjectName == projectName && x.ProjectToken == projectToken).FirstOrDefault();

        if (existingProjectInfo != null)
        {
            existingProjectInfo.LedgerVotingStartIndex = item.Value; // update with last known ledger index

        }
    }

}



//Retrieve data on already stored votings
Console.WriteLine("Connect to XRPL and retrieve votings");

//clean sheet, as such start 
var ctx = new CancellationTokenSource(new TimeSpan(1, 0, 0));
await foreach (var result in _votingManager.GetVotings(projectConfigurations, ctx, rippledServer))
{
    //result.VotingResultFile = string.Concat("https://", config["RemoteConfigHostBlobStorage"],"/",config["ConfigFolderName"], "/", result.ProjectName, "/", result.ProjectToken, "/", result.VotingId, "-", result.VotingStartIndex, "-", result.VotingEndIndex, ".json");
    storedVotingInformation.Add(result);
}

Console.WriteLine("Store retrieved votings");
if (storedVotingInformation is not null && storedVotingInformation.Count > 0)
{
    await _persistantStorageManager.Upload<List<Voting>>(config["ConfigFolderName"], "votingregistrations.json", storedVotingInformation, storageAccountBlobContainerKey);
}

Console.WriteLine("Get votings for which we need to get the results and publish them to a queue");

Dictionary<string, bool> FilesToCheckForExistance = new Dictionary<string, bool>();
storedVotingInformation?.ForEach(x =>
{
    // FilesToCheckForExistance.Add(string.Concat(string.Concat(x.ProjectName, "-", x.ProjectToken, "-", x.VotingId,"-", x.VotingStartIndex, ".json")), false);
    FilesToCheckForExistance.Add(string.Concat(string.Concat(x.ProjectName, "/", x.ProjectToken, "/", x.VotingId, "-", x.VotingStartIndex, "-", x.VotingEndIndex, ".json")), false);
});

await _persistantStorageManager.FilesExistCheck(config["ConfigFolderName"], FilesToCheckForExistance, storageAccountBlobContainerKey);

foreach (var outstandingVotings in FilesToCheckForExistance.Where(x => x.Value == false))
{
    var selectedVoting = storedVotingInformation.Where(x => (string.Concat(string.Concat(x.ProjectName, "/", x.ProjectToken, "/", x.VotingId, "-", x.VotingStartIndex, "-", x.VotingEndIndex, ".json")) == outstandingVotings.Key)).FirstOrDefault();
    if (selectedVoting is not null)
    {
        await _queueManager.QueueMessage<Voting>(selectedVoting, storageAccountQueueKey);
    }
}

Console.WriteLine("Call orchestrator which will start processing the voting results");
using (var client = new HttpClient())
{

    var result = await client.PostAsJsonAsync<VotingResultProcessorRequest>(config["VotingResultsProcessorEndpoint"], new VotingResultProcessorRequest() { instances = Convert.ToInt32(config["VotingResultsProcessorInstanceCount"]), location = config["VotingResultsProcessorLocation"] }, CancellationToken.None);
}


