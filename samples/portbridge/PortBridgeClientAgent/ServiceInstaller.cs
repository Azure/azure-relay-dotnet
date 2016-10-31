// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeClientAgent
{
    using System.ComponentModel;
    using System.Configuration.Install;

    [RunInstaller(true)]
    public partial class ServiceInstaller : Installer
    {
        public ServiceInstaller()
        {
            InitializeComponent();
        }
    }
}