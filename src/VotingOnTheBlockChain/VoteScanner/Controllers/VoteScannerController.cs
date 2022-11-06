using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Transactions;
using Common.Models;
using Dapr;
using Google.Protobuf.Reflection;

namespace VoteScanner.Controllers
{
    [ApiController]
    [Route("api/votescanner")]
    public class VoteScannerController : ControllerBase
    {

        /// <summary>
        /// State store name.
        /// </summary>
        public const string ConfigStoreName = "projects-config";

        public const string ProjectVotesStoreName = "projectvotes-config";

        private readonly ILogger<VoteScannerController> _logger;

        public VoteScannerController(ILogger<VoteScannerController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("projects")]
        public async Task<ActionResult<IEnumerable<ProjectConfig>>> Get()
        {
            DaprClient daprClient = new DaprClientBuilder().Build();
            _logger.LogInformation("Enter ProjectConfigurations");

            var configResult = await daprClient.GetStateAsync<List<ProjectConfig>>(ConfigStoreName, "xrpl");

            if (configResult is null)
            {
                return this.NotFound();
            }
            return configResult;
        }

        [HttpPost]
        [Route("projects")]
        public async Task<ActionResult<IEnumerable<ProjectConfig>>> Save(ProjectConfig[] projects)
        {
            DaprClient daprClient = new DaprClientBuilder().Build();
            _logger.LogInformation("Enter Save ProjectConfigurations");
            if(projects is null || projects.Count() == 0)
            {
                return this.BadRequest("No project configuration data submitted");
            }

            var state = await daprClient.GetStateEntryAsync<List<ProjectConfig>>(ConfigStoreName, "xrpl");

            if (state.Value is not null)
            {
                var updatedProjectconfiguration = new List<ProjectConfig>();
                updatedProjectconfiguration.AddRange(state.Value);
                var combinedResult = updatedProjectconfiguration.Union(projects);
                state.Value = combinedResult.ToList();
            }
            else
            {
                state.Value = projects.ToList();
            }

            await state.SaveAsync();
            return state.Value;
        }

        [HttpDelete]
        [Route("projects")]
        public async Task<ActionResult<bool>> Delete()
        {
            DaprClient daprClient = new DaprClientBuilder().Build();
            _logger.LogInformation("Delete ProjectConfigurations");

            try
            {
                await daprClient.DeleteStateAsync(ConfigStoreName, "xrpl");
            }
            catch(Exception)
            {
                return false;
            }


            return true;
        }

    }
}