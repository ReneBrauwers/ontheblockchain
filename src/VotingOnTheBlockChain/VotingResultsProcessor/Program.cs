using Common.Models.Config;
using Common.Models.Report;
using Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using VotingResultsProcessor.Services;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();




//obtain storage account key from environment variable
var storageAccountBlobContainerKey = config["STORAGE_ACCOUNT_BLOBCONTAINER_SIGNATURE"];
var jsonPayload = config["INSTRUCTIONS_JSON"];
//if(jsonPayload is null)
//{
//    jsonPayload = Environment.GetEnvironmentVariable("INSTRUCTIONS_JSON");
//}
var rippledServer = config["websocketAddress"];

//convert jsonPayload to Voting Object
var votingInformation = JsonSerializer.Deserialize<Voting>(jsonPayload);

Console.WriteLine($"Received payload: {jsonPayload} ");

//DI
var services = new ServiceCollection();


services.AddSingleton(x => new VotingManager(config));
services.AddSingleton(x => new VotingReportManager(config));
services.AddSingleton(x => new PersistantStorageManager(config));

var sp = services.BuildServiceProvider();
var _persistantStorageManager = sp.GetRequiredService<PersistantStorageManager>();
var _votingReportManager = sp.GetRequiredService<VotingReportManager>();
var _votingManager = sp.GetRequiredService<VotingManager>();
//var synchronisation = sp.GetRequiredService<votingsynchronisation>();
//var streaming = sp.GetRequiredService<sample>();


/* steps of execution
 * 1) Connect to XRPL and retrieve voting information
 * 2) Persist voting information in blob storage
 */


Console.WriteLine("Start process to create the voting Report");
var votingReport = await _votingReportManager.CreateVotingReport(votingInformation);

Console.WriteLine("Persist voting report");
var reportFileName = string.Concat(votingInformation.ProjectName,"-", votingInformation.ProjectToken,"-", votingInformation.VotingId, "-", votingInformation.VotingStartIndex, ".json");
await _persistantStorageManager.Upload<VotingResultReport>(config["ConfigFolderName"], reportFileName, votingReport, storageAccountBlobContainerKey);


  


