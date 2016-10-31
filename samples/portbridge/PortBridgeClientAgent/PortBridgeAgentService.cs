// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeClientAgent
{
    using System.ServiceProcess;

    partial class PortBridgeAgentService : ServiceBase
    {
        readonly PortBridgeClientForwarderHost host;

        public PortBridgeAgentService(PortBridgeClientForwarderHost host)
        {
            this.host = host;
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            host.Open();
        }

        protected override void OnStop()
        {
            host.Close();
        }
    }
}