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
                var queueLink = string.Concat("https://", _configuration["RemoteConfigHostQueueStorage"], "/", _configuration["Queuename"], "?", signingkey);
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

        public async Task<T> DeQueueMessage<T>(int count, string signingkey)
        {
            T? result = default;

            //get queue client service client
            var queueLink = string.Concat("https://", _configuration["RemoteConfigHostQueueStorage"], "/", _configuration["Queuename"], "?", signingkey);
            QueueClient queue = new QueueClient(new Uri(queueLink));
            var message = await queue.ReceiveMessageAsync();
            if (!message.GetRawResponse().IsError)
            {
                if (message.Value is null)
                {
                    return result;
                }

                result = message.Value.Body.ToObjectFromJson<T>();
                var deleteResult = await queue.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt);


            }
            else
            {
                Console.WriteLine($"Error occured dequeuing message: {message.GetRawResponse().Status} - {message.GetRawResponse().ReasonPhrase}");
            }




            return result;
        }


    }
}
