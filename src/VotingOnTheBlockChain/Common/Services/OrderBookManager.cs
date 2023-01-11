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
    public sealed class OrderBookManager
    {

      
        private string _currency { get; set; }
        private string _issuerAccount { get; set; }
        private OrderType _orderBookType { get; set; }
        private Uri _websocketServer { get; set; }

 
        /// <summary>
        /// Operation which retrieves the orderbook for a given token
        /// </summary>
        /// <param name="issuer">Token issuer account</param>
        /// <param name="currency">Token</param>
        /// <param name="orderBookType">Sell or Buy</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <param name="orderBookDepth">Depth of orders to return</param>
        /// <returns>Order book entries for the given issuer token</returns>
        public async Task<List<OrderBook>> GetOrderBook(string issuer, string currency, OrderType orderBookType, CancellationTokenSource cTokenSource, string socketEndpoint, int orderBookDepth = 5)
        {

            _issuerAccount = issuer;
            _currency = currency;
            _orderBookType = orderBookType;
            _websocketServer = new Uri(socketEndpoint);
            return await RetrieveOrderBook(orderBookDepth, cTokenSource.Token);
        }

        private async Task<List<OrderBook>> RetrieveOrderBook(int orderBookDepth, CancellationToken token)
        {
            dynamic takerGets = new ExpandoObject();
            dynamic takerPays = new ExpandoObject();

            dynamic xrplRequest = new ExpandoObject();


            switch (_orderBookType)
            {
                case OrderType.Buy:
                    {
                        takerGets.currency = "XRP";
                        takerPays.currency = _currency;
                        takerPays.issuer = _issuerAccount;

                        break;
                    }
                case OrderType.Sell:
                    {
                        takerPays.currency = "XRP";
                        takerGets.currency = _currency;
                        takerGets.issuer = _issuerAccount;

                        break;
                    }
            }
            using (var clientWebsocket = new ClientWebSocket())
            {
                xrplRequest.id = Guid.NewGuid().ToString();
                xrplRequest.command = "book_offers";
                xrplRequest.limit = orderBookDepth;
                xrplRequest.taker_gets = takerGets;
                xrplRequest.taker_pays = takerPays;

                await clientWebsocket.ConnectAsync(_websocketServer, token);
                await SendWebSocketRequest(clientWebsocket, JsonSerializer.Serialize(xrplRequest), token);

                //Set up receive
                var results = await ProcessOrderBook(clientWebsocket, xrplRequest, _orderBookType, token);

                return results;
            }



        }

        /// <summary>
        /// Looks up start and end date for all votings leveraging the _controllerAccount
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="originalRequest"></param>
        /// <returns></returns>
        private async Task<List<OrderBook>> ProcessOrderBook(ClientWebSocket socket, dynamic originalRequest, OrderType orderType, CancellationToken token)
        {
            List<OrderBook> Orders = new List<OrderBook>();
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
                                    var order = new OrderBook();
                                    order.Account = x.GetProperty("Account").GetString();

                                    //add logic
                                    if (orderType == OrderType.Sell)
                                    {
                                        order.Side = OrderType.Sell;

                                        if (x.TryGetProperty("TakerGets", out var takergets))
                                        {
                                            order.Volume = Convert.ToDecimal(takergets.GetProperty("value").GetString()); //amount to sell
                                            order.Total = (Convert.ToDecimal(x.GetProperty("TakerPays").GetString()) / 1000000); //amount to receive
                                            order.Currency = takergets.GetProperty("currency").GetString(); //currency to sell in
                                            order.Issuer = takergets.GetProperty("issuer").GetString(); // 								
                                            order.Price = (Convert.ToDecimal(x.GetProperty("quality").GetString()) / 1000000);
                                            order.OrderSummary = string.Concat("selling ", order.Volume.ToString("N2"), " ", order.Currency, " receiving ", order.Total.ToString("N2"), " XRP");
                                        }

                                    }

                                    if (orderType == OrderType.Buy)
                                    {
                                        order.Side = OrderType.Buy;
                                        if (x.TryGetProperty("TakerPays", out var takerpays))
                                        {

                                            order.Volume = (Convert.ToDecimal(x.GetProperty("TakerGets").GetString()) / 1000000); //amount paid
                                            order.Total = Convert.ToDecimal(takerpays.GetProperty("value").GetString()); //amount bought
                                            order.Currency = takerpays.GetProperty("currency").GetString(); //currency to buy
                                            order.Issuer = takerpays.GetProperty("issuer").GetString(); // 
                                            order.Price = order.Volume / order.Total;  //(xrp divided by RPR)
                                            order.OrderSummary = string.Concat("buying ", order.Total.ToString("N2"), " ", order.Currency, " costing ", order.Volume.ToString("N2"), " XRP");
                                        }

                                    }

                                    if (order is not null)
                                    {
                                        Orders.Add(order);
                                    }

                                    //await result = ExtractTransactionPayload(x.GetProperty("tx"), ledgerIndexMax); //indicates the current ledger_index at the moment of getting this data back
                                    return true;
                                }).ToList();

                                if (morePages)
                                {
                                    originalRequest.marker = marker;
                                    await SendWebSocketRequest(socket, JsonSerializer.Serialize(originalRequest), token);
                                    buffer = new ArraySegment<byte>(new byte[2048]);
                                }
                            }


                        }
                    }


                }
            } while (morePages);

            return Orders;
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
