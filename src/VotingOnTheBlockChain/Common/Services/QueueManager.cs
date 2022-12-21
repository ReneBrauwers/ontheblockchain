using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VotingScanner.Services
{
    public sealed class QueueManager
    {
        protected readonly IConfiguration _configuration;

        public QueueManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        public async Task<bool> QueueMessage<T>(T jsonContents, string signingkey)
        {
            bool isError = true;
            try
            {
                //get queue client service client
                var queueLink = string.Concat("https://", _configuration["RemoteConfigHostQueueStorage"],"/", _configuration["Queuename"], "?", signingkey);
                QueueClient queue = new QueueClient(new Uri(queueLink));
                var result = await queue.SendMessageAsync(BinaryData.FromString(JsonSerializer.Serialize(jsonContents)));
                isError = result.GetRawResponse().IsError;
            }
            catch
            {
                return false;
            }

            return isError;
        }


    }
}
