using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Common.Services
{
    public sealed class PersistantStorageManager
    {
        protected readonly IConfiguration _configuration;

        public PersistantStorageManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> Upload<T>(string containerName,string fileName, T jsonContents, string signingkey)
        {
            bool isError = true;
            try
            {
                //get blob service client
                var containerLink = string.Concat("https://", _configuration["RemoteConfigHostBlobStorage"], "?", signingkey);
                BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(containerLink));

                // Get the container client
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                //get blob client

                var blobClient = containerClient.GetBlobClient(fileName);
                if (blobClient == null)
                {
                    var result = await containerClient.UploadBlobAsync(fileName, BinaryData.FromString(JsonSerializer.Serialize(jsonContents)));
                    isError = result.GetRawResponse().IsError;
                }
                else
                {
                    var result = await blobClient.UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(jsonContents)), true);
                    isError = result.GetRawResponse().IsError;
                }



            }
            catch
            {
                return false;
            }

            return isError;
        }

        public async Task<T> Download<T>(string containerName, string fileName,string signingkey)
        {
            T? returnValue = default;
             
                bool isError = true;
                //get blob service client
                var containerLink = string.Concat("https://", _configuration["RemoteConfigHostBlobStorage"], "?", signingkey);
                BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(containerLink));

                // Get the container client
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                //get blob client
                var blobClient = containerClient.GetBlobClient(fileName);
                if (blobClient is not null)
                {
                    var result = await blobClient.DownloadContentAsync();
                    isError = result.GetRawResponse().IsError;
                    if(!isError && result.Value.Content is not null)
                    {
                        returnValue = result.Value.Content.ToObjectFromJson<T>();
                    }
                }             

            return returnValue;


        }   

        public async Task FilesExistCheck(string containerName, Dictionary<string, bool> fileNames, string signingkey)
        {
           
            //get blob service client
            var containerLink = string.Concat("https://", _configuration["RemoteConfigHostBlobStorage"], "?", signingkey);
            BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(containerLink));

            // Get the container client
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);           
            var result = containerClient.GetBlobsAsync(BlobTraits.All,BlobStates.All).AsPages();

            // Enumerate the blobs returned for each page.
            await foreach (Azure.Page<BlobItem> blobPage in result)
            {
                foreach (BlobItem blobItem in blobPage.Values)
                {
                    if(fileNames.ContainsKey(blobItem.Name))
                    {
                        fileNames[blobItem.Name] = true;                       
                    }
                }
                               
            }

            


        }

    }
}
