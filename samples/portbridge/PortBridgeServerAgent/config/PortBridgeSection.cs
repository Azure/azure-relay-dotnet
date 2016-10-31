// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridgeServerAgent
{
    using System.Configuration;

    class PortBridgeSection : ConfigurationSection
    {
        internal const string hostMappingsString = "hostMappings";
        internal const string localHostName = "localHostName";
        internal const string serviceBusNamespaceString = "serviceBusNamespace";
        internal const string serviceBusAccessRuleNameString = "serviceBusAccessRuleName";
        internal const string serviceBusAccessRuleKeyString = "serviceBusAccessRuleKey";

        [ConfigurationProperty(serviceBusNamespaceString, DefaultValue = null, IsRequired = true)]
        public string ServiceNamespace
        {
            get { return (string) this[serviceBusNamespaceString]; }
            set { this[serviceBusNamespaceString] = value; }
        }

        [ConfigurationProperty(serviceBusAccessRuleNameString, DefaultValue = "owner", IsRequired = false)]
        public string AccessRuleName
        {
            get { return (string) this[serviceBusAccessRuleNameString]; }
            set { this[serviceBusAccessRuleNameString] = value; }
        }

        [ConfigurationProperty(serviceBusAccessRuleKeyString, DefaultValue = null, IsRequired = true)]
        public string AccessRuleKey
        {
            get { return (string) this[serviceBusAccessRuleKeyString]; }
            set { this[serviceBusAccessRuleKeyString] = value; }
        }

        [ConfigurationProperty(localHostName, DefaultValue = null, IsRequired = false)]
        public string LocalHostName
        {
            get { return (string) this[localHostName]; }
            set { this[localHostName] = value; }
        }

        [ConfigurationProperty(hostMappingsString, IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (HostMappingCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public HostMappingCollection HostMappings
        {
            get { return (HostMappingCollection) base[hostMappingsString]; }
        }
    }
}