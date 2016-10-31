// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeServerAgent
{
    using System.ComponentModel;
    using System.Configuration.Install;

    [RunInstaller(true)]
    public partial class ServiceInstaller : Installer
    {
        void serviceProcessInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
        }
    }
}