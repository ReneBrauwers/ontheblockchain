using Common.Extensions;
using Common.Models.Account;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Common.Extensions.Enums;

namespace Common.Services
{
    public sealed class AccountOfferManager
    {

        private string _holderAccount { get; set; } = string.Empty; //Account to lookup the orders for
        private Uri _websocketServer { get; set; } = new Uri("https://localhost");
        private int _ledgerIndex { get; set; } = 0;


        /// <summary>
        /// Operation which will retrieve account offer information
        /// </summary>
        /// <param name="account">Account to lookup</param>
        /// <param name="ledgerIndex">Point in time for the lookup</param>
        /// <param name="cTokenSource">Cancellation token source</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns>Order book entries for the given account</returns>
        public async Task<List<AccountOrderBook>> GetAccountOrder(string account, int ledgerIndex, CancellationTokenSource cTokenSource, string socketEndpoint = "wss://xrplcluster.com/") //string projectId, string projectName, string controllerAccount, string votingAccount, string issuerAccount)
        {
            _holderAccount = account;
            _websocketServer = new Uri(socketEndpoint);
            _ledgerIndex = ledgerIndex;
            return await RetrieveAccountOffers(cTokenSource.Token);
        }

        private async Task<List<AccountOrderBook>> RetrieveAccountOffers(CancellationToken token)
        {

            using (var clientWebsocket = new ClientWebSocket())
            {

                dynamic xrplRequest = new ExpandoObject();
                xrplRequest.id = Guid.NewGuid().ToString();
                xrplRequest.command = "account_offers";
                xrplRequest.account = _holderAccount;
                if (_ledgerIndex > 0)
                {
                    xrplRequest.ledger_index = _ledgerIndex;
                }

                await clientWebsocket.ConnectAsync(_websocketServer, token);
                await SendWebSocketRequest(clientWebsocket, JsonSerializer.Serialize(xrplRequest), token);

                //Set up receive
                var results = await ProcessAccountOrders(clientWebsocket, xrplRequest,token);

                return results;
            }



        }


        private async Task SendWebSocketRequest(ClientWebSocket socket, string data, CancellationToken cToken)
        {

            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, cToken);
        }

        private async Task SendWebSocketRequest(ClientWebSocket socket, string data)
        {

            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }


        /// <summary>
        /// Looks up start and end date for all votings leveraging the _controllerAccount
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="originalRequest"></param>
        /// <returns></returns>
        private async Task<List<AccountOrderBook>> ProcessAccountOrders(ClientWebSocket socket, dynamic originalRequest, CancellationToken token)
        {
            List<AccountOrderBook> orderBookEntries = new List<AccountOrderBook>();
            dynamic marker = new ExpandoObject();

            bool morePages = true;

            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
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
                            var responseJson = JsonDocument.Parse(responseMessage);

                            if (responseJson != null && responseJson.RootElement.GetProperty("status").ValueEquals("success"))
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


                                //iterate over all transactions, and populate / update the List<registrations>
                                jsonResult.RootElement.GetProperty("offers").EnumerateArray().Select(x =>
                                {
                                    //determine sell / buy
                                    OrderType orderType = OrderType.Undefined;
                                    if (x.TryGetProperty("taker_gets", out var sellObject))
                                    {
                                        if (sellObject.ValueKind == JsonValueKind.String)
                                        {
                                            //it's a buy
                                            orderType = OrderType.Buy;

                                        }
                                        else
                                        {
                                            //it's a sell
                                            orderType = OrderType.Sell;
                                        }
                                    }

                                    switch (orderType)
                                    {
                                        case OrderType.Buy:
                                            {
                                                var buyEntry = new AccountOrderBook();
                                                buyEntry.Account = jsonResult.RootElement.GetProperty("account").GetString();
                                                buyEntry.Currency = x.GetProperty("taker_pays").GetProperty("currency").GetString().HexToString();
                                                buyEntry.Issuer = x.GetProperty("taker_pays").GetProperty("issuer").GetString().HexToString();
                                                buyEntry.Volume = double.Parse(x.GetProperty("taker_pays").GetProperty("value").GetString());
                                                buyEntry.Side = OrderType.Buy;
                                                buyEntry.Total = (double.Parse(x.GetProperty("taker_gets").GetString()) / 1000000);
                                                buyEntry.Price = (buyEntry.Total / buyEntry.Volume);
                                                buyEntry.OrderSummary = string.Concat("buying ", buyEntry.Volume.ToString("N2"), " ", buyEntry.Currency, " costing ", buyEntry.Total.ToString("N2"), " XRP");
                                                orderBookEntries.Add(buyEntry);
                                                break;
                                            }
                                        case OrderType.Sell:
                                            {
                                                var sellEntry = new AccountOrderBook();
                                                sellEntry.Account = jsonResult.RootElement.GetProperty("account").GetString();
                                                sellEntry.Currency = x.GetProperty("taker_gets").GetProperty("currency").GetString().HexToString();
                                                sellEntry.Issuer = x.GetProperty("taker_gets").GetProperty("issuer").GetString().HexToString();
                                                sellEntry.Volume = double.Parse(x.GetProperty("taker_gets").GetProperty("value").GetString());
                                                sellEntry.Side = OrderType.Sell;
                                                sellEntry.Total = (double.Parse(x.GetProperty("taker_pays").GetString()) / 1000000);
                                                sellEntry.Price = (double.Parse(x.GetProperty("quality").GetString()) / 1000000);
                                                sellEntry.OrderSummary = string.Concat("selling ", sellEntry.Volume.ToString("N2"), " ", sellEntry.Currency, " receiving ", sellEntry.Total.ToString("N2"), " XRP");
                                                orderBookEntries.Add(sellEntry);
                                                break;
                                            }


                                    }
                                    //await result = ExtractTransactionPayload(x.GetProperty("tx"), ledgerIndexMax); //indicates the current ledger_index at the moment of getting this data back
                                    return true;
                                }).ToList();

                                if (morePages)
                                {
                                    originalRequest.marker = marker;
                                    await SendWebSocketRequest(socket, JsonSerializer.Serialize(originalRequest),token);
                                    buffer = new ArraySegment<byte>(new byte[2048]);
                                }
                            }


                        }
                    }


                }
            } while (morePages);

            return orderBookEntries;
        }





    }
}
