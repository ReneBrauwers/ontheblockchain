using Common.Models.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using System.Dynamic;
using Common.Extensions;

namespace Common.Services
{
    public class VotingManager
    {

       
   
        /// <summary>
        /// Operation which will retrieve the last voting found
        /// </summary>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <returns></returns>
        public async Task<VotingRegistrations> GetLastVoting(ProjectConfig projectConfigurationSettings, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
           var _options = new JsonSerializerOptions()
           { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);
            VotingRegistrations voting = new VotingRegistrations();
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(socketUrl, cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "account_tx";
                xrplRequest.account = projectConfigurationSettings.ControllerAccount; //account responsible for initiating a vote start and vote end

                //indicates to move back in time
                xrplRequest.forward = false;


                //open connection
                if (client.State != WebSocketState.Open)
                {
                    await client.ConnectAsync(socketUrl, cTokenSource.Token);
                }

                //send request
                var requestData = System.Text.Json.JsonSerializer.Serialize(xrplRequest, _options);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);

                //receive
                dynamic marker = new ExpandoObject();
                bool morePages = false;


                var buffer = new ArraySegment<byte>(new byte[2048]);

                do
                {
                    try
                    {
                        WebSocketReceiveResult result;
                        using (var ms = new MemoryStream())
                        {
                            do
                            {
                                result = await client.ReceiveAsync(buffer, cTokenSource.Token);
                                ms.Write(buffer.Array, buffer.Offset, result.Count);

                            } while (!result.EndOfMessage);

                            if (result.MessageType == WebSocketMessageType.Close)
                                break;

                            ms.Seek(0, SeekOrigin.Begin);
                            using (var reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                var responseMessage = await reader.ReadToEndAsync();

                                if (!string.IsNullOrWhiteSpace(responseMessage))
                                {
                                    var responseJson = JsonDocument.Parse(responseMessage);

                                    if (responseJson != null)
                                    {
                                        var jsonResult = JsonDocument.Parse(responseJson.RootElement.GetProperty("result").ToString());

                                        //check for marker at result.marker                               
                                        var markerElements = jsonResult.RootElement.EnumerateObject().Where(x => x.NameEquals("marker"));
                                        if (markerElements != null && markerElements.Count() > 0)
                                        {
                                            //determine type                                    
                                            marker = markerElements.First().Value;
                                            morePages = true;

                                        }
                                        else
                                        {
                                            morePages = false;
                                        }


                                        if (responseJson.RootElement.GetProperty("status").ValueEquals("success"))
                                        {



                                            

                                            if (jsonResult.RootElement.GetProperty("transactions").GetArrayLength() > 0)
                                            {
                                                //var transactionString = jsonResult.RootElement.GetProperty("transactions").GetRawText();
                                                var transactions = jsonResult.RootElement.GetProperty("transactions").EnumerateArray().AsEnumerable();
                                                var counter = 0;

                                                //we will exit whenever we have found a START voting
                                               

                                                foreach (var txs in transactions)
                                                {
                                                    counter++;

                                                    var tx = txs.GetProperty("tx");
                                                    if (tx.TryGetProperty("Memos", out var MemoJsonElement))
                                                    {
                                                        if (MemoJsonElement.ValueKind == JsonValueKind.Array)
                                                        {

                                                            if (tx.GetProperty("Memos").GetArrayLength() > 0)
                                                            {
                                                                var memosCount = tx.GetProperty("Memos").GetArrayLength();
                                                                if (memosCount > 2) // we have a new voting
                                                                {
                                                                   
                                                                    var votingId = tx.GetProperty("Memos")[0].GetProperty("Memo").GetProperty("MemoData").GetString();
                                                                    voting.VotingStartIndex = tx.GetProperty("ledger_index").GetUInt32();
                                                                    voting.VotingId = votingId;
                                                                    voting.VotingName = votingId.HexToString();
                                                                    voting.ProjectToken = projectConfigurationSettings.ProjectToken;
                                                                    voting.ProjectName = projectConfigurationSettings.ProjectName;
                                                                    voting.VotingDataReference = String.Empty;
                                                                    if (voting.VotingEndIndex > 0)
                                                                    {
                                                                        voting.IsLive = false;
                                                                    }
                                                                    else
                                                                    {
                                                                        voting.VotingEndIndex = 0;
                                                                        voting.IsLive = true;
                                                                    }
                                                                    voting.VotingAccount = tx.GetProperty("Destination").GetString();
                                                                    voting.VotingControllerAccount = projectConfigurationSettings.ControllerAccount;
                                                                    //get voting options
                                                                    var votingOptions = tx.GetProperty("Memos").EnumerateArray().AsEnumerable().Skip(1).Select(x => x.GetProperty("Memo").GetProperty("MemoData").GetString().HexToString()).ToArray();
                                                                    voting.VotingOptions = votingOptions;

                                                                    //let's exit
                                                                    morePages = false;
                                                                    cTokenSource.Cancel();
                                                                    break;

                                                                }
                                                                if (memosCount == 2) //should be an end
                                                                {
                                                                    if (tx.GetProperty("Memos")[1].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString().ToUpper().Contains("ENDS"))
                                                                    {
                                                                        if (voting is not null)
                                                                        {
                                                                           
                                                                            voting.VotingEndIndex = tx.GetProperty("ledger_index").GetUInt32();
                                                                            voting.IsLive = false;


                                                                        }
                                                                        
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }


                                                }


                                            }

                                        }

                                    }
                                    else
                                    {
                                        //ensure we exit
                                        morePages = false;
                                    }




                                    if (morePages)
                                    {

                                        xrplRequest.marker = marker;
                                        requestData = System.Text.Json.JsonSerializer.Serialize(xrplRequest, _options);


                                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);
                                        //buffer = new ArraySegment<byte>(new byte[2048]);
                                    }
                                }


                            }
                        }



                    }
                    catch (Exception ex)
                    {
                        morePages = false;

                    }
                } while (morePages);









            }

            return voting;
        }

    }
}
