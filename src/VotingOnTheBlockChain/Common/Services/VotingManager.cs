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
using Microsoft.Extensions.Primitives;
using Common.Models.Report;
using Common.Models.Account;
using static Common.Extensions.Enums;
using System.Runtime.CompilerServices;
using System.Net;

namespace Common.Services
{
    public sealed class VotingManager
    {

        /// <summary>
        /// Operation which will retrieve the all completed votings given a certain start range
        /// </summary>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <returns></returns>
        public async IAsyncEnumerable<Voting> GetVotings(List<ProjectConfig> projectConfigurationSettings, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
             
            Voting voting;
            var _options = new JsonSerializerOptions()
            { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(socketUrl, cTokenSource.Token);

                foreach (var project in projectConfigurationSettings)
                {
                  
                    uint indexStopIndicator = project.LedgerVotingStartIndex;


                    //create request
                    dynamic xrplRequest = new ExpandoObject();
                    //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                    xrplRequest.id = Guid.NewGuid();
                    xrplRequest.command = "account_tx";
                    xrplRequest.account = project.ControllerAccount; //account responsible for initiating a vote start and vote end

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
                        //try
                        //{
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
                                    voting = new Voting();
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
                                               


                                                foreach (var txs in transactions)
                                                {                                                   
                                                   
                                                    var tx = txs.GetProperty("tx");
                                                   
                                                    //only proceed whilst the retrieved data has not surpassed the start index from the project config
                                                    if (indexStopIndicator < tx.GetProperty("ledger_index").GetUInt32())
                                                    {

                                                        if (tx.TryGetProperty("Memos", out var MemoJsonElement))
                                                        {
                                                            if (MemoJsonElement.ValueKind == JsonValueKind.Array)
                                                            {

                                                                if (tx.GetProperty("Memos").GetArrayLength() > 0)
                                                                {
                                                                    var memosCount = tx.GetProperty("Memos").GetArrayLength();
                                                                    if (memosCount > 2) // we have a new voting
                                                                    {
                                                                        //as we are travelling back in time, we should have a end-voting instruction first; if not skip; as this voting 
                                                                        //appears to be a live one.
                                                                        if (voting is not null && voting.VotingEndIndex > 0)
                                                                        {

                                                                            var votingId = tx.GetProperty("Memos")[0].GetProperty("Memo").GetProperty("MemoData").GetString();
                                                                            voting.VotingStartIndex = tx.GetProperty("ledger_index").GetUInt32();
                                                                            voting.VotingId = votingId;
                                                                            voting.VotingName = votingId.HexToString();
                                                                            voting.ProjectToken = project.ProjectToken;
                                                                            voting.ProjectName = project.ProjectName;
                                                                            voting.VotingDataReference = String.Empty;
                                                                            voting.IssuerAccount = project.IssuerAccount;
                                                                            voting.VotingAccount = tx.GetProperty("Destination").GetString();
                                                                            voting.VotingControllerAccount = project.ControllerAccount;
                                                                            //get voting options
                                                                            var votingOptions = tx.GetProperty("Memos").EnumerateArray().AsEnumerable().Skip(1).Select(x => x.GetProperty("Memo").GetProperty("MemoData").GetString().HexToString()).ToArray();
                                                                            voting.VotingOptions = votingOptions;

                                                                            //yield the result back
                                                                            
                                                                            yield return voting;
                                                                            voting = new();
                                                                           
                                                                        }

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
                                                    else
                                                    {
                                                        //we exit
                                                        morePages = false;
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



                        //}
                        //catch (Exception ex)
                        //{
                        //    morePages = false;
                        //
                        //}
                    } while (morePages);



                }





            }

            //return voting;
        }

        /// <summary>
        /// Operation which will retrieve the last voting found
        /// </summary>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <returns></returns>
        public async Task<Voting> GetVoting(ProjectConfig projectConfigurationSettings, uint startIndex, uint endIndex ,CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
            var _options = new JsonSerializerOptions()
            { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);
            Voting voting = new Voting();
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(socketUrl, cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "account_tx";
                xrplRequest.ledger_index_min = startIndex;
                xrplRequest.ledger_index_max = endIndex;
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
                                                                    voting.IssuerAccount = projectConfigurationSettings.IssuerAccount;
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



        /// <summary>
        /// Operation which will retrieve the last voting found
        /// </summary>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <returns></returns>
        public async Task<Voting> GetLastVoting(ProjectConfig projectConfigurationSettings, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
            var _options = new JsonSerializerOptions()
            { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);
            Voting voting = new Voting();
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
                                                                    voting.IssuerAccount = projectConfigurationSettings.IssuerAccount;
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

        /// <summary>
        /// Operation which retrieves all cast votes from the XRPL
        /// </summary>
        /// <param name="votingAccount">Address used to cast votes on</param>
        /// <param name="votingControllerAccount">Account which sends control messages indicating start and end of a voting</param>
        /// <param name="votingId">Voting Id, used to check for correct voting</param>/// 
        /// <param name="startIndex">Start Ledger Index, indicates when voting started</param>
        /// <param name="endIndex">End Ledger Index, indicates when voting has ended</param>
        /// <returns></returns>
        public async IAsyncEnumerable<VotingResults> GetVotingResults(string votingAccount, string votingControllerAccount, string votingId, uint startIndex, uint endIndex, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
            //List<VotingResults> votingResults = new();

            if (startIndex <= 0 || endIndex <= 0)
            {
                throw new NotSupportedException("GetVotingResults operation requires a start and end index to be larger than 0");
            }

            var _options = new JsonSerializerOptions()
            { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(socketUrl, cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "account_tx";
                xrplRequest.account = votingAccount;
                xrplRequest.ledger_index_min = startIndex;
                xrplRequest.ledger_index_max = endIndex;

                //indicates to get old items first
                xrplRequest.forward = true;


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

                while (!cTokenSource.IsCancellationRequested)
                {
                    // Note that the received block might only be part of a larger message. If this applies in your scenario,
                    // check the received.EndOfMessage and consider buffering the blocks until that property is true.
                    // Or use a higher-level library such as SignalR.

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
                                var successResponse = responseJson.RootElement.GetProperty("status").ValueEquals("success");
                                var correlationId = responseJson.RootElement.GetProperty("id").ToString();

                                if (responseJson != null && successResponse)
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



                                    var transactionsEnumerator = jsonResult.RootElement.GetProperty("transactions").EnumerateArray();
                                    if (transactionsEnumerator.Count() > 0)
                                    {

                                        //iterate over all transactions, and populate / update the List<registrations>
                                        bool votingHasEnded = false;

                                        foreach (var transaction in transactionsEnumerator)
                                        {
                                            if (!votingHasEnded)
                                            {
                                                var voteResult = new VotingResults();

                                                if (transaction.TryGetProperty("tx", out var txs))
                                                {
                                                    //only get items with memos
                                                    if (txs.TryGetProperty("Memos", out var memoField))
                                                    {
                                                        var totalMemos = memoField.EnumerateArray().Count();

                                                        switch (totalMemos)
                                                        {
                                                            case 2: //we have a new voting
                                                                {
                                                                    //check matching voting id
                                                                    if (memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString() == votingId.HexToString())
                                                                    {

                                                                        var address = txs.GetProperty("Account").GetString();
                                                                        //add check if we received an END notification
                                                                        bool containsEnds = memoField[1].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString().ToUpper().Contains("ENDS");

                                                                        bool isFromControllerAccount = (address == votingControllerAccount ? true : false);
                                                                        bool isNotAtsameLedgerAsStart = (txs.GetProperty("inLedger").GetUInt32() != xrplRequest.ledger_index_min);
                                                                        if (containsEnds && isFromControllerAccount)
                                                                        {
                                                                            votingHasEnded = true;

                                                                        }
                                                                        else
                                                                        {
                                                                            voteResult.Vote = memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString();
                                                                            voteResult.VoteId = memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString();
                                                                            voteResult.VoterAddress = address;
                                                                            voteResult.VoterChoice = memoField[1].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString();
                                                                            voteResult.VoteRegistrationIndex = txs.GetProperty("inLedger").GetUInt32();
                                                                            voteResult.VoteRegistrationDateTime = txs.GetProperty("date").GetInt32().rippleEpochToDateUTC();
                                                                            yield return voteResult;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        var address = txs.GetProperty("Account").GetString();
                                                                        var id = memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString();

                                                                    }

                                                                    break;
                                                                }
                                                            case >= 3:
                                                                {
                                                                    break;
                                                                }
                                                            default:
                                                                break;
                                                        }
                                                    }
                                                }
                                            }
                                        }


                                    }
                                    else //no transactions found, check if we need to exit as we've hit the endledger index
                                    {
                                        //additional check
                                        if (morePages)
                                        {
                                            if (!cTokenSource.Token.CanBeCanceled) //can not be cancelled
                                            {
                                                morePages = false;
                                            }
                                        }
                                    }


                                    if (morePages)
                                    {
                                        xrplRequest.marker = marker;
                                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(xrplRequest, _options))), WebSocketMessageType.Text, true, cTokenSource.Token);
                                        //await SendWebSocketRequest(socket, JsonSerializer.Serialize(originalRequest));

                                    }
                                    else
                                    {
                                        cTokenSource.Cancel();
                                    }



                                }
                            }
                        }
                    }
                }





            }


        }

        /// <summary>
        /// Operation which retrieves all cast votes from the XRPL
        /// </summary>
        /// <param name="votingAccount">Address used to cast votes on</param>
        /// <param name="votingControllerAccount">Account which sends control messages indicating start and end of a voting</param>
        /// <param name="startIndex">Start Ledger Index, indicates when voting started</param>

        /// <returns></returns>
        public async IAsyncEnumerable<VotingResults> GetVotingResults(string votingAccount, string votingControllerAccount, uint startIndex, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/")
        {
            //List<VotingResults> votingResults = new();

            if (startIndex <= 0)
            {
                throw new NotSupportedException("GetVotingResults operation requires a start index to be larger than 0");
            }

            var _options = new JsonSerializerOptions()
            { PropertyNameCaseInsensitive = true };

            Uri socketUrl = new Uri(socketEndpoint);

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(socketUrl, cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "account_tx";
                xrplRequest.account = votingAccount;
                xrplRequest.ledger_index_min = startIndex;
                xrplRequest.ledger_index_max = -1;

                //indicates to get old items first
                xrplRequest.forward = true;


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

                while (!cTokenSource.IsCancellationRequested)
                {
                    // Note that the received block might only be part of a larger message. If this applies in your scenario,
                    // check the received.EndOfMessage and consider buffering the blocks until that property is true.
                    // Or use a higher-level library such as SignalR.

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
                                var successResponse = responseJson.RootElement.GetProperty("status").ValueEquals("success");
                                var correlationId = responseJson.RootElement.GetProperty("id").ToString();

                                if (responseJson != null && successResponse)
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



                                    var transactionsEnumerator = jsonResult.RootElement.GetProperty("transactions").EnumerateArray();
                                    if (transactionsEnumerator.Count() > 0)
                                    {

                                        //iterate over all transactions, and populate / update the List<registrations>
                                        bool votingHasEnded = false;

                                        foreach (var transaction in transactionsEnumerator)
                                        {
                                            if (!votingHasEnded)
                                            {
                                                var voteResult = new VotingResults();

                                                if (transaction.TryGetProperty("tx", out var txs))
                                                {
                                                    //only get items with memos
                                                    if (txs.TryGetProperty("Memos", out var memoField))
                                                    {
                                                        var totalMemos = memoField.EnumerateArray().Count();

                                                        switch (totalMemos)
                                                        {
                                                            case 2: //we have a new voting
                                                                {
                                                                    var address = txs.GetProperty("Account").GetString();
                                                                    //add check if we received an END notification
                                                                    bool containsEnds = memoField[1].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString().ToUpper().Contains("ENDS");

                                                                    bool isFromControllerAccount = (address == votingControllerAccount ? true : false);
                                                                    bool isNotAtsameLedgerAsStart = (txs.GetProperty("inLedger").GetUInt32() != xrplRequest.ledger_index_min);
                                                                    if (containsEnds && isFromControllerAccount)
                                                                    {
                                                                        votingHasEnded = true;

                                                                    }
                                                                    else
                                                                    {
                                                                        voteResult.Vote = memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString();
                                                                        voteResult.VoteId = memoField[0].GetProperty("Memo").GetProperty("MemoData").GetString();
                                                                        voteResult.VoterAddress = address;
                                                                        voteResult.VoterChoice = memoField[1].GetProperty("Memo").GetProperty("MemoData").GetString().HexToString();
                                                                        voteResult.VoteRegistrationIndex = txs.GetProperty("inLedger").GetUInt32();
                                                                        voteResult.VoteRegistrationDateTime = txs.GetProperty("date").GetInt32().rippleEpochToDateUTC();
                                                                        yield return voteResult;
                                                                    }

                                                                    break;
                                                                }
                                                            case >= 3:
                                                                {


                                                                    break;
                                                                }
                                                            default:
                                                                break;
                                                        }
                                                    }
                                                }
                                            }
                                        }


                                    }
                                    else //no transactions found, check if we need to exit as we've hit the endledger index
                                    {
                                        //additional check
                                        if (morePages)
                                        {
                                            if (!cTokenSource.Token.CanBeCanceled) //can not be cancelled
                                            {
                                                morePages = false;
                                            }
                                        }
                                    }


                                    if (morePages)
                                    {
                                        xrplRequest.marker = marker;
                                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(xrplRequest, _options))), WebSocketMessageType.Text, true, cTokenSource.Token);
                                        //await SendWebSocketRequest(socket, JsonSerializer.Serialize(originalRequest));

                                    }
                                    else
                                    {
                                        cTokenSource.Cancel();
                                    }



                                }
                            }
                        }
                    }
                }





            }


        }



        /// <summary>
        /// Operation which retrieves an account balance snapshot at a given ledger index
        /// </summary>

        /// <param name="Addresses">Accounts to retrieve balances off</param>
        /// <param name="PeerAccount">Project Token Issuer account to use to lookup the balance</param>
        /// <param name="LedgerGetBalanceIndex">Snapshot point in time</param>
        /// <param name="socketEndpoint"></param>
        /// <param name="cTokenSource">Cancellation token</param>/// 
        /// <returns></returns>
        public async IAsyncEnumerable<AccountBalance> GetVoterBalancesAsync(List<string> Addresses, string PeerAccount, uint LedgerGetBalanceIndex, string Currency, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com")
        {

            // UInt32 votingHasConcludedLedgerIndex;


            //var voteRegistrations = new List<VoterAccountBalance>();
            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };

            using (var clientWebsocket = new ClientWebSocket())
            {
                await clientWebsocket.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);
                //uint activeRequestsSend = 0;
                dynamic xrplRequest = new ExpandoObject();
                foreach (var address in Addresses)
                {
                    //create request

                    xrplRequest = new ExpandoObject();
                    xrplRequest.id = address;
                    xrplRequest.command = "account_lines";
                    xrplRequest.account = address;
                    if (!string.IsNullOrWhiteSpace(PeerAccount))
                    {
                        xrplRequest.peer = PeerAccount;
                    }

                    if (LedgerGetBalanceIndex > 0)
                    {
                        xrplRequest.ledger_index = LedgerGetBalanceIndex;  //popularVotingResult.InLedgerTransaction;
                    }

                    //open connection
                    if (clientWebsocket.State != WebSocketState.Open)
                    {
                        await clientWebsocket.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);
                    }


                    //send request                    
                    await SendWebSocketRequest(clientWebsocket, JsonSerializer.Serialize(xrplRequest, options), cTokenSource.Token);



                    //await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);


                    //activeRequestsSend++;
                }

                //Set up receive
                Dictionary<string, bool> workItems = new Dictionary<string, bool>();
                Addresses.ForEach(x => workItems[x] = false);
                await foreach (var result in ProcessAccountBalance(clientWebsocket, PeerAccount, LedgerGetBalanceIndex, Currency, workItems, cTokenSource.Token))
                {

                    yield return result;
                }



            }





        }


        private async IAsyncEnumerable<AccountBalance> ProcessAccountBalance(ClientWebSocket socket, string peerAccount, uint ledgerGetBalanceIndex, string currency, Dictionary<string, bool> WorkItems, CancellationToken token)
        {



            dynamic marker = new ExpandoObject();
            bool morePages = true;

            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                var itemsLeft = WorkItems.Where(x => x.Value == false).Count();

                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, token);
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
                            AccountBalance accountBalanceResult = new AccountBalance();
                            var responseJson = JsonDocument.Parse(responseMessage);
                            var correlationId = responseJson.RootElement.GetProperty("id").ToString();

                            //init return value;
                            //accountBalanceResult = new AccountBalance();
                            accountBalanceResult.Balance = 0;
                            accountBalanceResult.Currency = currency;
                            accountBalanceResult.IsValid = false;
                            accountBalanceResult.InvalidReason = string.Empty;



                            if (responseJson != null && responseJson.RootElement.GetProperty("status").ValueEquals("success"))
                            {
                                var accountBalanceValid = false;
                                var accountBalanceInvalidReason = string.Empty;

                                var jsonResult = JsonDocument.Parse(responseJson.RootElement.GetProperty("result").ToString());
                                accountBalanceResult.Address = jsonResult.RootElement.GetProperty("account").GetString();
                                accountBalanceResult.LedgerIndex = jsonResult.RootElement.GetProperty("ledger_index").GetUInt32();


                                //Process 
                                if (correlationId == accountBalanceResult.Address)
                                {
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

                                    var trustLineFound = false;
                                    if (jsonResult.RootElement.GetProperty("lines").GetArrayLength() > 0)
                                    {
                                        var arrayLength = jsonResult.RootElement.GetProperty("lines").GetArrayLength();

                                        for (int i = 0; i < arrayLength; i++)
                                        {
                                            var accountLineCurrency = jsonResult.RootElement.GetProperty("lines")[i].GetProperty("currency").GetString().HexToString();
                                            if (accountLineCurrency.ToLower() == currency.ToLower())
                                            {
                                                morePages = false; //not need to continue on the next page, we already found a hit!

                                                trustLineFound = true;
                                                var accountBalanceInfo = Decimal.Parse(jsonResult.RootElement.GetProperty("lines")[i].GetProperty("balance").GetString(), System.Globalization.NumberStyles.Float);
                                                accountBalanceResult.Balance = accountBalanceInfo;

                                                if (accountBalanceInfo > 0)
                                                {
                                                    accountBalanceValid = true;
                                                }
                                                else
                                                {
                                                    accountBalanceValid = false;
                                                    accountBalanceInvalidReason = $"Voting balance is {accountBalanceInfo}";
                                                }

                                            }
                                        }

                                        if (!trustLineFound)
                                        {
                                            accountBalanceValid = false;
                                            accountBalanceInvalidReason = $"{currency} trustline not found";
                                        }

                                    }
                                    else
                                    {
                                        accountBalanceValid = false;
                                        accountBalanceInvalidReason = "No trustlines found";

                                        // activeRequestsSend--;
                                    }


                                    if (morePages)
                                    {
                                        dynamic xrplRequest = new ExpandoObject();
                                        xrplRequest.id = correlationId;
                                        xrplRequest.command = "account_lines";
                                        xrplRequest.account = correlationId;
                                        if (!string.IsNullOrWhiteSpace(peerAccount))
                                        {
                                            xrplRequest.peer = peerAccount;
                                        }

                                        if (ledgerGetBalanceIndex > 0)
                                        {
                                            xrplRequest.ledger_index = ledgerGetBalanceIndex;  //popularVotingResult.InLedgerTransaction;
                                        }

                                        xrplRequest.marker = marker;
                                        await SendWebSocketRequest(socket, JsonSerializer.Serialize(xrplRequest), token);
                                        buffer = new ArraySegment<byte>(new byte[2048]);
                                    }
                                    else
                                    {
                                        //check for a recorded IsFaulty
                                        accountBalanceResult.IsValid = accountBalanceValid;
                                        accountBalanceResult.InvalidReason = accountBalanceInvalidReason;

                                        if (WorkItems.TryGetValue(correlationId, out bool processingResult))
                                        {
                                            WorkItems[correlationId] = true; //indicates we completed it
                                        }

                                        yield return accountBalanceResult;
                                    }
                                }
                            }
                            else
                            {
                                //get ID and remove
                                if (WorkItems.TryGetValue(correlationId, out bool processingResult))
                                {
                                    WorkItems[correlationId] = true; //indicates we completed it; although there was an error
                                    accountBalanceResult.Address = correlationId;
                                    accountBalanceResult.IsValid = false;
                                    accountBalanceResult.InvalidReason = "Invalid response from validator";
                                }
                            }


                        }
                    }


                }
            } while (WorkItems.Where(x => x.Value == false).Count() > 0);


        }


        private async Task SendWebSocketRequest(ClientWebSocket socket, string data, CancellationToken cToken)
        {

            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, cToken);
        }

        private async Task SendWebSocketRequest(ClientWebSocket socket, string data)
        {

            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
