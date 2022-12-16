using Common.Extensions;
using Common.Models.Ledger;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Common.Services
{
    public sealed class LedgerManager
    {


        /// <summary>
        /// Operation which will retrieve the last closed ledger index
        /// </summary>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<Ledger> GetLastLedgerIndex(CancellationTokenSource cTokenSource,  string socketEndpoint = "wss://xrplcluster.com")
        {
          var ledger = new Ledger();
            ledger.ledgerIndex = 0;
            ledger.ledgerCloseTimeUTC= DateTime.MinValue;
           


            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "ledger";
                xrplRequest.ledger_index = "validated";
                xrplRequest.full = false;
                xrplRequest.accounts = false;
                xrplRequest.transactions = false;
                xrplRequest.expand = false;
                xrplRequest.owner_funds = false;


                //open connection
                if (client.State != WebSocketState.Open)
                {
                    await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);
                }

                //send request
                var requestData = System.Text.Json.JsonSerializer.Serialize(xrplRequest, options);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);

 

                var buffer = new ArraySegment<byte>(new byte[2048]);

                while (!cTokenSource.Token.IsCancellationRequested)
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
                                var successResponse = responseJson.RootElement.GetProperty("status").ValueEquals("success");
                                //var correlationId = responseJson.RootElement.GetProperty("id").ToString();

                                if (responseJson != null && successResponse)
                                {
                                    var jsonResult = JsonDocument.Parse(responseJson.RootElement.GetProperty("result").ToString());

                                    //get index
                                    if (jsonResult.RootElement.GetProperty("ledger").ValueKind == JsonValueKind.Object)
                                    {

                                        ledger.ledgerIndex = (uint)jsonResult.RootElement.GetProperty("ledger_index").GetInt32();
                                        ledger.ledgerCloseTimeUTC = jsonResult.RootElement.GetProperty("ledger").GetProperty("close_time").GetInt32().rippleEpochToDateUTC();

                                        

                                       

                                            //let's finish up we have a match
                                            cTokenSource.Cancel();
                                         
                                      
                                       

                                    }
                                    else
                                    {
                                        cTokenSource.Cancel();
                                    }



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

            return ledger;
        }

        /// <summary>
        /// Operation which will retrieve the last closed ledger index
        /// </summary>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<Ledger> GetSpecificLedgerIndex(CancellationTokenSource cTokenSource, uint ledgerIndex, string socketEndpoint = "wss://xrplcluster.com")
        {
            var ledger = new Ledger();
            ledger.ledgerIndex = ledgerIndex;
            ledger.ledgerCloseTimeUTC = DateTime.MinValue;



            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "ledger";
                xrplRequest.ledger_index = ledger.ledgerIndex;
                xrplRequest.full = false;
                xrplRequest.accounts = false;
                xrplRequest.transactions = false;
                xrplRequest.expand = false;
                xrplRequest.owner_funds = false;


                //open connection
                if (client.State != WebSocketState.Open)
                {
                    await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);
                }

                //send request
                var requestData = System.Text.Json.JsonSerializer.Serialize(xrplRequest, options);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);



                var buffer = new ArraySegment<byte>(new byte[2048]);

                while (!cTokenSource.Token.IsCancellationRequested)
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
                                var successResponse = responseJson.RootElement.GetProperty("status").ValueEquals("success");
                                //var correlationId = responseJson.RootElement.GetProperty("id").ToString();

                                if (responseJson != null && successResponse)
                                {
                                    var jsonResult = JsonDocument.Parse(responseJson.RootElement.GetProperty("result").ToString());

                                    //get index
                                    if (jsonResult.RootElement.GetProperty("ledger").ValueKind == JsonValueKind.Object)
                                    {

                                        ledger.ledgerIndex = (uint)jsonResult.RootElement.GetProperty("ledger_index").GetInt32();
                                        ledger.ledgerCloseTimeUTC = jsonResult.RootElement.GetProperty("ledger").GetProperty("close_time").GetInt32().rippleEpochToDateUTC();





                                        //let's finish up we have a match
                                        cTokenSource.Cancel();




                                    }
                                    else
                                    {
                                        cTokenSource.Cancel();
                                    }



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

            return ledger;
        }


        /// <summary>
        /// Operation which will retrieve a ledger index based for a given datetime
        /// </summary>
        /// <param name="fromDate">Desired dateTime used to calculate the closest ledger index</param>
        /// <param name="allowedTollerance">Allowed deviation allowed when retrieving the closest ledger index</param>
        /// <param name="averageLedgerCloseTime">averat time needed to close a ledger on the xrpl, default is 4 seconds</param>
        /// <param name="cTokenSource">Token source allowing a controlled cancellation</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns>Ledger Index Information</returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<Ledger> GetLedgerIndexFromDate(DateTime fromDate, TimeSpan allowedTollerance, CancellationTokenSource cTokenSource, TimeSpan? averageLedgerCloseTime,  string socketEndpoint = "wss://xrplcluster.com")
        {

            var ledger = new Ledger();
            ledger.ledgerIndex = 0;
            ledger.ledgerCloseTimeUTC = DateTime.MinValue;

            if (fromDate <= new DateTime(2000, 1, 1))
            {
                throw new NotSupportedException("Date can not be prior to January 1,2000");
            }
            int ledgerIndex = 0;
            int ledgerCloseTime = 0;
            int fromDateRippleEpoch = fromDate.rippleEpochFromDateUTC();
            int estimatedLedgerIndexPast = 0;
            if (averageLedgerCloseTime == TimeSpan.Zero)
            {
                averageLedgerCloseTime = new TimeSpan(0, 0, 4); // 4 seconds;
            }

          
            var options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };

            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);

                //create request
                dynamic xrplRequest = new ExpandoObject();
                //List<string> accounts = new List<string>() { _selectedValue.VotingAccount };

                xrplRequest.id = Guid.NewGuid();
                xrplRequest.command = "ledger";
                xrplRequest.ledger_index = "validated";
                xrplRequest.full = false;
                xrplRequest.accounts = false;
                xrplRequest.transactions = false;
                xrplRequest.expand = false;
                xrplRequest.owner_funds = false;


                //open connection
                if (client.State != WebSocketState.Open)
                {
                    await client.ConnectAsync(new Uri(socketEndpoint), cTokenSource.Token);
                }

                //send request
                var requestData = System.Text.Json.JsonSerializer.Serialize(xrplRequest, options);
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(requestData)), WebSocketMessageType.Text, true, cTokenSource.Token);


                //receive
                dynamic marker = new ExpandoObject();
                bool morePages = false;

                var buffer = new ArraySegment<byte>(new byte[2048]);

                while (!cTokenSource.Token.IsCancellationRequested)
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
                                var successResponse = responseJson.RootElement.GetProperty("status").ValueEquals("success");
                                //var correlationId = responseJson.RootElement.GetProperty("id").ToString();

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

                                    //get index

                                    if (jsonResult.RootElement.GetProperty("ledger").ValueKind == JsonValueKind.Object)
                                    {

                                        ledgerIndex = jsonResult.RootElement.GetProperty("ledger_index").GetInt32();
                                        ledgerCloseTime = jsonResult.RootElement.GetProperty("ledger").GetProperty("close_time").GetInt32();

                                        var ledgerCloseTimeUTC = ledgerCloseTime.rippleEpochToDateUTC();

                                        var ledgerTimeDifferenceInSeconds = ledgerCloseTime - fromDateRippleEpoch;
                                        // Console.WriteLine($"Ledger time difference in seconds: {ledgerTimeDifferenceInSeconds}");

                                        //determine tollerance
                                        var lowerTimeFrameTollerance = fromDate.Subtract(allowedTollerance);
                                        var upperTimeFrameTollerance = fromDate.Add(allowedTollerance);

                                        if (ledgerCloseTimeUTC >= lowerTimeFrameTollerance && ledgerCloseTimeUTC <= upperTimeFrameTollerance)
                                        {
                                            //let's finish up we have a match
                                            ledger.ledgerCloseTimeUTC = ledgerCloseTimeUTC;
                                            ledger.ledgerIndex = (uint)ledgerIndex;
                                            cTokenSource.Cancel();
                                            morePages = false;
                                        }
                                        else
                                        {

                                            if (ledgerTimeDifferenceInSeconds > 0)
                                            {
                                                // we need to calculate the difference and fire off a new request
                                                var LedgerIndexesToDeduct = Convert.ToInt32((ledgerCloseTime - fromDateRippleEpoch) / averageLedgerCloseTime?.TotalSeconds);
                                                estimatedLedgerIndexPast = ledgerIndex - LedgerIndexesToDeduct;
                                                morePages = true;
                                            }
                                            else
                                            {
                                                //future, not allowed finish up
                                                cTokenSource.Cancel();
                                                morePages = false;
                                            }


                                        }

                                    }
                                    else
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
                                        xrplRequest.ledger_index = estimatedLedgerIndexPast;
                                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(xrplRequest, options))), WebSocketMessageType.Text, true, cTokenSource.Token);
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

                return ledger;

            }
        }

    }
}
