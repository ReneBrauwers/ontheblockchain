using Blazored.LocalStorage;
using Common.Extensions;
using Common.Models.Config;
using Common.Models.Report;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using System.Data.SqlTypes;
using System.Net.Http.Json;
using System.Xml;

namespace Common.Services
{
    public sealed class CookieManager
    {
        protected readonly IConfiguration _appConfig;
        protected IJSRuntime _JS;



        public CookieManager(IConfiguration configuration, IJSRuntime JS)
        {
            _appConfig = configuration;
            _JS = JS;

        }

        public async Task<RippledServer> GetRippledServer()
        {           
            //get cookies
            var _activeRippleNetwork = await _JS.InvokeAsync<string>("getCookie", "rippledNetwork");
            var _activeRippledServer = await _JS.InvokeAsync<string>("getCookie", "rippledServer");

            //cookie can have a null string value, so perform a check as well for this.
            if (string.IsNullOrWhiteSpace(_activeRippleNetwork) || string.IsNullOrWhiteSpace(_activeRippledServer) || _activeRippledServer == "null" || _activeRippleNetwork == "null")
            {
                var availableRippledServers = _appConfig.GetValue<string>("rippledServersMain")?.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                _activeRippleNetwork = "Main";
                _activeRippledServer = availableRippledServers?[0];
                await _JS.InvokeVoidAsync("setCookie", "rippledNetwork", _activeRippleNetwork, 365);
                await _JS.InvokeVoidAsync("setCookie", "rippledServer", _activeRippledServer, 365);

            }
            return new RippledServer()
            {
                Server = _activeRippledServer,
                Network = _activeRippleNetwork,
                IsConnected = false
            };

        }

        public async Task UpdateRippledServer(RippledServer server)
        {
            await _JS.InvokeVoidAsync("setCookie", "rippledNetwork", server.Network, 365);
            await _JS.InvokeVoidAsync("setCookie", "rippledServer", server.Server, 365);
        }
    }
 

     
    
   
}
