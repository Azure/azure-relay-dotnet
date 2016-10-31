// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeServerAgent
{
    using System.Configuration;

    public class HostMappingElement : ConfigurationElement
    {
        internal const string targetHostString = "targetHost";
        internal const string allowedPortsString = "allowedPorts";
        internal const string allowedPipesString = "allowedPipes";

        public HostMappingElement(string targetHost, string allowedPorts, string allowedPipes)
        {
            TargetHost = targetHost;
            AllowedPorts = allowedPorts;
            AllowedPipes = allowedPipes;
        }

        public HostMappingElement()
        {
        }

        [ConfigurationProperty(targetHostString, DefaultValue = "localhost", IsRequired = false, IsKey = true)]
        public string TargetHost
        {
            get { return (string) this[targetHostString]; }
            set { this[targetHostString] = value; }
        }

        [ConfigurationProperty(allowedPortsString, DefaultValue = "*", IsRequired = false)]
        public string AllowedPorts
        {
            get { return (string) this[allowedPortsString]; }
            set { this[allowedPortsString] = value; }
        }

        [ConfigurationProperty(allowedPipesString, DefaultValue = "", IsRequired = false)]
        public string AllowedPipes
        {
            get { return (string) this[allowedPipesString]; }
            set { this[allowedPipesString] = value; }
        }
    }
}