// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.


namespace PortBridgeServerAgent
{
    using System.ServiceProcess;

    partial class PortBridgeService : ServiceBase
    {
        readonly PortBridgeServiceForwarderHost host;

        public PortBridgeService(PortBridgeServiceForwarderHost host)
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