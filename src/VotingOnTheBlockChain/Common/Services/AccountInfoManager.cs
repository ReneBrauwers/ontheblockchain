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
    public sealed class AccountInfoManager
    {
     
        private string _holderAccount { get; set; } = string.Empty; //Account to lookup the orders for
        private Uri _websocketServer { get; set; } = new Uri("https://localhost");

        private int _ledgerIndex { get; set; } = 0;

        private string _socketEndpoint { get; set; } = string.Empty;
        private RippledServerState _rippledServerState { get; set; }

        public AccountInfoManager(RippledServerState rippledServerState)
        {
            _rippledServerState = rippledServerState;
        }

        /// <summary>
        /// Operation which will retrieve basic account information
        /// </summary>
        /// <param name="account">Account to lookup</param>
        /// <param name="ledgerIndex">Point in time for the lookup</param>
        /// <param name="cTokenSource">Cancellation token source</param>
        /// <param name="socketEndpoint">Rippled Server endpoint</param>
        /// <returns>Order book entries for the given account</returns>
        public async Task<AccountInformation> GetAccountInformation(string account, int ledgerIndex, CancellationTokenSource cTokenSource, string socketEndpoint) //string projectId, string projectName, string controllerAccount, string votingAccount, string issuerAccount)
        {
            _holderAccount = account;
            _websocketServer = new Uri(socketEndpoint);
            _socketEndpoint = socketEndpoint;
            _ledgerIndex = ledgerIndex;
            var result = await RetrieveAccountInfo(cTokenSource.Token);
            return result;
        }
        private async Task<AccountInformation> RetrieveAccountInfo(CancellationToken token)
        {
            var results = new AccountInformation();
            using (var clientWebsocket = new ClientWebSocket())
            {

                dynamic xrplRequest = new ExpandoObject();
                xrplRequest.id = Guid.NewGuid().ToString();
                xrplRequest.command = "account_info";
                xrplRequest.account = _holderAccount;
                xrplRequest.strict = true;
                xrplRequest.queue = false;
                if (_ledgerIndex > 0)
                {
                    xrplRequest.ledger_index = _ledgerIndex;
                }

                await clientWebsocket.ConnectAsync(_websocketServer, token);
                //sent event
                _rippledServerState.UpdateServerConnectionState(_socketEndpoint, true);
                await SendWebSocketRequest(clientWebsocket, JsonSerializer.Serialize(xrplRequest), token);

                //Set up receive
                results = await ProcessAccountInfo(clientWebsocket, xrplRequest, token);

               
            }

            //sent event
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
        private async Task<AccountInformation> ProcessAccountInfo(ClientWebSocket socket, dynamic originalRequest, CancellationToken token)
        {
            AccountInformation accountInformation = new();
            bool morePages = false;

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
                               
                                    var jsonResult = JsonDocument.Parse(responseJson.RootElement.GetProperty("result").GetProperty("account_data").ToString());
                                    var accountData = jsonResult.RootElement;

                                    accountInformation.Alias = string.Empty;
                                    accountInformation.Balance = Decimal.Parse(accountData.GetProperty("Balance").ToString());
                                    accountInformation.Account = accountData.GetProperty("Account").ToString();
                                    









                            }
                            else
                            {
                                accountInformation = null;
                                morePages = false;
                            }


                        }
                    }


                }
            } while (morePages);

            return accountInformation;
        }


    }
}
