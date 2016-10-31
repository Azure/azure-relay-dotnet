// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeServerAgent
{
    using System.Collections.Generic;
    using PortBridge;

    class PortBridgeServiceForwarderHost
    {
        public PortBridgeServiceForwarderHost()
        {
            Forwarders = new List<ServiceConnectionForwarder>();
        }

        public List<ServiceConnectionForwarder> Forwarders { get; }

        public void Open()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.OpenService();
            }
        }

        public void Close()
        {
            foreach (var forwarder in Forwarders)
            {
                forwarder.CloseService();
            }
        }
    }
}