using Common.Extensions;
using Common.Handlers;
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
        private string _socketEndpoint { get; set;} = string.Empty;
        private int _ledgerIndex { get; set; } = 0;

        private RippledServerState _rippledServerState { get; set; }

        public AccountOfferManager(RippledServerState rippledServerState)
        {
            _rippledServerState = rippledServerState;
        }

        /// <summary>
        /// Operation which will retrieve account offer information
        /// </summary>
        /// <param name="account">Account to lookup</param>
        /// <param name="ledgerIndex">Point in time for the lookup</param>
        /// <param name="cTokenSource">Cancellation token source</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns>Order book entries for the given account</returns>
        public async Task<List<AccountOffers>> GetAccountOrder(string account, int ledgerIndex, CancellationTokenSource cTokenSource, string socketEndpoint) //string projectId, string projectName, string controllerAccount, string votingAccount, string issuerAccount)
        {
            _holderAccount = account;
            _websocketServer = new Uri(socketEndpoint);
            _socketEndpoint = socketEndpoint;
            _ledgerIndex = ledgerIndex;
            return await RetrieveAccountOffers(cTokenSource.Token);
        }

        private async Task<List<AccountOffers>> RetrieveAccountOffers(CancellationToken token)
        {
            var results = new List<AccountOffers>();
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
                //sent event
                _rippledServerState.UpdateServerConnectionState(_socketEndpoint, true);
                await SendWebSocketRequest(clientWebsocket, JsonSerializer.Serialize(xrplRequest), token);

                //Set up receive
                results = await ProcessAccountOrders(clientWebsocket, xrplRequest,token);

               
            }
            _rippledServerState.UpdateServerConnectionState(_socketEndpoint, false);
            return results;


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
        private async Task<List<AccountOffers>> ProcessAccountOrders(ClientWebSocket socket, dynamic originalRequest, CancellationToken token)
        {
            List<AccountOffers> orderBookEntries = new List<AccountOffers>();
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
                                    var entry = new AccountOffers();
                                    entry.Account = jsonResult.RootElement.GetProperty("account").GetString();
                                    //OrderType typeOfOrder = OrderType.Undefined;

                                    JsonElement takerGets;
                                    JsonElement takerPays;
                                    bool isTakerGetsObject = false;
                                    bool isTakerPaysObject = false;
                                    if (x.TryGetProperty("taker_gets", out takerGets))
                                    {
                                        switch(takerGets.ValueKind)
                                        {
                                            case JsonValueKind.Object:
                                                {
                                                    isTakerGetsObject = true;
                                                    break;
                                                }
                                            default:
                                                {
                                                    isTakerGetsObject = false;
                                                    break;
                                                }
                                        }
                                    }
                                    if (x.TryGetProperty("taker_pays", out takerPays))
                                    {
                                        switch (takerPays.ValueKind)
                                        {
                                            case JsonValueKind.Object:
                                                {
                                                    isTakerPaysObject = true;
                                                    break;
                                                }
                                            default:
                                                {
                                                    isTakerPaysObject = false;
                                                    break;
                                                }
                                        }
                                    }
                                    
                                    if (isTakerGetsObject && isTakerPaysObject)
                                    {
                                        
                                        //get is sell and pay is buy
                                        entry.TypeOfOrder = OrderType.Swap;
                                        entry.InAmount = decimal.Parse(x.GetProperty("taker_gets").GetProperty("value").GetString(), System.Globalization.NumberStyles.Float);
                                        entry.InCurrency = x.GetProperty("taker_gets").GetProperty("currency").GetString().HexToString();
                                        entry.OutCurrency = x.GetProperty("taker_pays").GetProperty("currency").GetString().HexToString();
                                        entry.OutAmount = decimal.Parse(x.GetProperty("taker_pays").GetProperty("value").GetString(), System.Globalization.NumberStyles.Float);
                                        entry.ExchangeRateVal = entry.OutAmount / entry.InAmount;
                                        entry.ExchangeRate = $"{entry.InCurrency}/{entry.OutCurrency}";

                                    }
                                    else if (isTakerPaysObject) //taker_gets XRP and sells taker_pays; thus account is * buying a token and selling XRP
                                    {
                                        //TODO: switch around Quantity + Offer
                                        //taker_gets is buy amount in XRP
                                        //entry.TypeOfOrder = OrderType.Sell;
                                        entry.TypeOfOrder = OrderType.Buy;
                                        entry.InCurrency = x.GetProperty("taker_pays").GetProperty("currency").GetString().HexToString();
                                        entry.InAmount = decimal.Parse(x.GetProperty("taker_pays").GetProperty("value").GetString(), System.Globalization.NumberStyles.Float);
                                        entry.OutAmount = decimal.Parse(x.GetProperty("taker_gets").GetString(), System.Globalization.NumberStyles.Float) / 1000000;
                                        entry.OutCurrency = "XRP";
                                        entry.ExchangeRateVal = entry.InAmount / entry.OutAmount;
                                        entry.ExchangeRate = $"{entry.InCurrency}/{entry.OutCurrency}";
                                    }
                                    else //taker_pays XRP and sells taker_gets; thus account is * selling a token and receiving XRP
                                    {
                                        //TODO: switch around Quantity + Offer
                                        //entry.TypeOfOrder = OrderType.Buy;
                                        entry.TypeOfOrder = OrderType.Sell;
                                        entry.OutCurrency = "XRP";
                                        entry.OutAmount = decimal.Parse(x.GetProperty("taker_pays").GetString(), System.Globalization.NumberStyles.Float) / 1000000;
                                        entry.InAmount = decimal.Parse(x.GetProperty("taker_gets").GetProperty("value").GetString(), System.Globalization.NumberStyles.Float);
                                        entry.InCurrency = x.GetProperty("taker_gets").GetProperty("currency").GetString().HexToString();
                                        entry.ExchangeRateVal = entry.OutAmount / entry.InAmount;
                                        entry.ExchangeRate = $"{entry.OutCurrency}/{entry.InCurrency}";
                                    }

                                    if (entry is not null && entry.TypeOfOrder != OrderType.Undefined)
                                    {
                                        orderBookEntries.Add(entry);
                                    }
                  
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
