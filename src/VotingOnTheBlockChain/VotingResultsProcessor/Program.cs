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


//DI
var services = new ServiceCollection();
services.AddSingleton(x => new VotingReportManager(config));

var sp = services.BuildServiceProvider();

var _votingReportManager = sp.GetRequiredService<VotingReportManager>();

//var synchronisation = sp.GetRequiredService<votingsynchronisation>();
//var streaming = sp.GetRequiredService<sample>();


/* steps of execution
 * 1) Connect to queue and retrieve information
 * 2) Persist voting information in blob storage
 */


Console.WriteLine("Start process to create the voting Report");
var cts = new CancellationTokenSource(new TimeSpan(1, 0, 0));
await _votingReportManager.Start(cts);

 Console.WriteLine("Processing finished, shutting down");
 


  


