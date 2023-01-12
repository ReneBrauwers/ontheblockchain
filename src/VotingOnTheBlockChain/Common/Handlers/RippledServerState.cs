using Common.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Handlers
{
    public sealed class RippledServerState
    {
        public RippledServer _rippledServer { get; private set; }
        public event Action? OnChange;

        public void SetRippledServerState(string network, string server)
        {
            if(_rippledServer is null)
            {
                _rippledServer= new RippledServer();
            }

            _rippledServer.Network = network;
            _rippledServer.Server = server; 
            _rippledServer.IsConnected = false;

            NotifyStateChanged();
        }

        public void UpdateServerConnectionState(string server, bool isConnected) 
        {
            if (_rippledServer is null)
            {
                _rippledServer = new RippledServer();
            }

            if (server == _rippledServer.Server)
            {

                _rippledServer.IsConnected = isConnected;
                _rippledServer.Server = server;

                NotifyStateChanged();
            }
        }
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
