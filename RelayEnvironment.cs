//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Relay
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Xml;

    class RelayEnvironment
    {
        public const string RelayEnvEnvironmentVariable = "RELAYENV";
        public const string StsEnabledEnvironmentVariable = "RELAYSTSENABLED";

        const int DefaultHttpPort = 80;
        const int DefaultHttpsPort = 443;
        const int DefaultNmfPort = 9354;

        static readonly MutableEnvironment Environment;

        static RelayEnvironment()
        {
            // first we check if "RELAYENV" environment variable is set
            string relayEnv = System.Environment.GetEnvironmentVariable(RelayEnvEnvironmentVariable);
            if (relayEnv != null)
            {
                switch (relayEnv.ToUpperInvariant())
                {
                    case "LIVE":
                        Environment = new MutableEnvironment(new LiveEnvironment());
                        break;
                    case "PPE":
                        Environment = new MutableEnvironment(new PpeEnvironment());
                        break;
                    case "BVT":
                        Environment = new MutableEnvironment(new BvtEnvironment());
                        break;
                    case "INT":
                        Environment = new MutableEnvironment(new IntEnvironment());
                        break;
                    case "LOCAL":
                        Environment = new MutableEnvironment(new LocalEnvironment());
                        break;
                    case "CUSTOM":
                        Environment = new MutableEnvironment(new CustomEnvironment());
                        break;
                    default:
                        Environment = new MutableEnvironment(new LiveEnvironment());
                        string error = string.Format(CultureInfo.InvariantCulture, "Invalid RELAYENV value: {0}, valid values = LIVE, PPE, INT", relayEnv);
                        EventLog.WriteEntry("MSCSH", error, EventLogEntryType.Error, 0);
                        break;
                }

                return;
            }

            // then we check if servicebus.config is present in .NET FW config folder
            ConfigSettings configSettings = new ConfigSettings();
            if (configSettings.HaveSettings)
            {
                Environment = new MutableEnvironment(configSettings);
                return;
            }

            // lastly we fall back to the default environment
            Environment = new MutableEnvironment(new LiveEnvironment());
        }

        public static string RelayHostRootName
        {
            get { return Environment.RelayHostRootName; }
            set { Environment.RelayHostRootName = value; }
        }

        public static int RelayHttpPort
        {
            get { return Environment.RelayHttpPort; }
        }

        public static int RelayHttpsPort
        {
            get { return Environment.RelayHttpsPort; }
        }

        public static string StsHostName
        {
            get { return Environment.StsHostName; }
        }

        public static bool StsEnabled
        {
            get { return Environment.StsEnabled; }
            set { Environment.StsEnabled = value; }
        }

        public static int StsHttpPort
        {
            get { return Environment.StsHttpPort; }
        }

        public static int StsHttpsPort
        {
            get { return Environment.StsHttpsPort; }
        }

        public static int RelayNmfPort
        {
            get
            {
                return Environment.RelayNmfPort;
            }
        }

        internal static string RelayPathPrefix
        {
            get { return Environment.RelayPathPrefix; }
        }

        public static bool GetEnvironmentVariable(string variable, bool defaultValue)
        {
            string variableString = System.Environment.GetEnvironmentVariable(variable);
            if (variableString != null)
            {
                bool returnValue;
                if (bool.TryParse(variableString, out returnValue))
                {
                    return returnValue;
                }
            }

            return defaultValue;
        }

        public static int GetEnvironmentVariable(string variable, int defaultValue)
        {
            string variableString = System.Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(variableString))
            {
                int returnValue;
                if (int.TryParse(variableString, out returnValue))
                {
                    return returnValue;
                }
            }

            return defaultValue;
        }

        interface IEnvironment
        {
            string RelayHostRootName { get; }
            int RelayHttpPort { get; }
            int RelayHttpsPort { get; }
            string RelayPathPrefix { get; }
            string StsHostName { get; }
            bool StsEnabled { get; }
            int StsHttpPort { get; }
            int StsHttpsPort { get; }
            int RelayNmfPort { get; }
        }

        class MutableEnvironment : IEnvironment
        {
            string relayHostRootName;
            int relayHttpPort;
            int relayHttpsPort;
            string relayPathPrefix;
            string stsHostName;
            bool stsEnabled;
            int stsHttpPort;
            int stsHttpsPort;
            int relayNmfPort;

            public MutableEnvironment(IEnvironment environment)
            {
                this.relayHostRootName = environment.RelayHostRootName;
                this.relayHttpPort = environment.RelayHttpPort;
                this.relayHttpsPort = environment.RelayHttpsPort;
                this.relayPathPrefix = environment.RelayPathPrefix;
                this.stsHostName = environment.StsHostName;
                this.stsEnabled = environment.StsEnabled;
#if DEBUG
                this.stsEnabled = GetEnvironmentVariable(StsEnabledEnvironmentVariable, this.stsEnabled);
#endif
                this.stsHttpPort = environment.StsHttpPort;
                this.stsHttpsPort = environment.StsHttpsPort;
                this.relayNmfPort = environment.RelayNmfPort;
            }

            public string RelayHostRootName { get { return this.relayHostRootName; } set { this.relayHostRootName = value; } }
            public int RelayHttpPort { get { return this.relayHttpPort; } }
            public int RelayHttpsPort { get { return this.relayHttpsPort; } }
            public string RelayPathPrefix { get { return this.relayPathPrefix; } }
            public string StsHostName { get { return this.stsHostName; } }
            public bool StsEnabled { get { return this.stsEnabled; } set { this.stsEnabled = value; } }
            public int StsHttpPort { get { return this.stsHttpPort; } }
            public int StsHttpsPort { get { return this.stsHttpsPort; } }
            public int RelayNmfPort { get { return this.relayNmfPort; } }
        }

        abstract class EnvironmentBase : IEnvironment
        {
            public abstract string RelayHostRootName { get; }
            public virtual int RelayHttpPort { get { return DefaultHttpPort; } }
            public virtual int RelayHttpsPort { get { return DefaultHttpsPort; } }
            public virtual string RelayPathPrefix { get { return string.Empty; } }
            public abstract string StsHostName { get; }
            public virtual bool StsEnabled { get { return true; } }
            public virtual int StsHttpPort { get { return DefaultHttpPort; } }
            public virtual int StsHttpsPort { get { return DefaultHttpsPort; } }
            public int RelayNmfPort { get { return DefaultNmfPort; } }
        }

        class LabsEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.appfabriclabs.com"; } }
            public override string StsHostName { get { return "accesscontrol.appfabriclabs.com"; } }
        }

        class LiveEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.windows.net"; } }
            public override string StsHostName { get { return "accesscontrol.windows.net"; } }
        }

        class PpeEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.windows-ppe.net"; } }
            public override string StsHostName { get { return "accesscontrol.windows-ppe.net"; } }
        }

        class BvtEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.windows-bvt.net"; } }
            public override string StsHostName { get { return "accesscontrol.windows-ppe.net"; } }
        }

        class IntEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.windows-int.net"; } }
            public override string StsHostName { get { return "accesscontrol.windows-ppe.net"; } }
        }

        class LocalEnvironment : EnvironmentBase
        {
            public override string RelayHostRootName { get { return "servicebus.onebox.windows-int.net"; } }
            public override string StsHostName { get { return "servicebus.onebox.windows-int.net"; } }
            public override bool StsEnabled { get { return false; } }
        }

        class CustomEnvironment : IEnvironment
        {
            const string RelayHostEnvironmentVariable = "RELAYHOST";
            const string RelayHttpPortEnvironmentVariable = "RELAYHTTPPORT";
            const string RelayHttpsPortEnvironmentVariable = "RELAYHTTPSPORT";
            const string RelayNmfPortEnvironmentVariable = "RELAYNMFPORT";
            const string RelayPathPrefixEnvironmentVariable = "RELAYPATHPREFIX";
            const string StsHostEnvironmentVariable = "STSHOST";
            const string StsHttpPortEnvironmentVariable = "STSHTTPPORT";
            const string StsHttpsPortEnvironmentVariable = "STSHTTPSPORT";

            string relayHostRootName;
            int relayHttpPort;
            int relayHttpsPort;
            string relayPathPrefix;
            string stsHostName;
            bool stsEnabled;
            int stsHttpPort;
            int stsHttpsPort;
            int relayNmfPort;

            public CustomEnvironment()
            {
                this.relayHostRootName = System.Environment.GetEnvironmentVariable(RelayHostEnvironmentVariable);
                this.relayHttpPort = GetEnvironmentVariable(RelayHttpPortEnvironmentVariable, DefaultHttpPort);
                this.relayHttpsPort = GetEnvironmentVariable(RelayHttpsPortEnvironmentVariable, DefaultHttpsPort);
                this.relayPathPrefix = System.Environment.GetEnvironmentVariable(RelayPathPrefixEnvironmentVariable);
                this.stsHostName = System.Environment.GetEnvironmentVariable(StsHostEnvironmentVariable);
#if DEBUG
                this.stsEnabled = GetEnvironmentVariable(StsEnabledEnvironmentVariable, true);
#else
                this.stsEnabled = true;
#endif
                this.stsHttpPort = GetEnvironmentVariable(StsHttpPortEnvironmentVariable, DefaultHttpPort);
                this.stsHttpsPort = GetEnvironmentVariable(StsHttpsPortEnvironmentVariable, DefaultHttpsPort);
                this.relayNmfPort = GetEnvironmentVariable(RelayNmfPortEnvironmentVariable, DefaultNmfPort);
            }

            public string RelayHostRootName { get { return this.relayHostRootName; } }
            public int RelayHttpPort { get { return this.relayHttpPort; } }
            public int RelayHttpsPort { get { return this.relayHttpsPort; } }
            public string RelayPathPrefix { get { return this.relayPathPrefix; } }
            public string StsHostName { get { return this.stsHostName; } }
            public bool StsEnabled { get { return this.stsEnabled; } }
            public int StsHttpPort { get { return this.stsHttpPort; } }
            public int StsHttpsPort { get { return this.stsHttpsPort; } }
            public int RelayNmfPort { get { return this.relayNmfPort; } }
        }

        class ConfigSettings : IEnvironment
        {
            const string RelayHostNameElement = "relayHostName";
            const string RelayHttpPortNameElement = "relayHttpPort";
            const string RelayHttpsPortNameElement = "relayHttpsPort";
            const string RelayNmfPortNameElement = "relayNmfPort";
            const string RelayPathPrefixElement = "relayPathPrefix";
            const string StsHostNameElement = "stsHostName";
            const string StsEnabledElement = "stsEnabled";
            const string StsHttpPortNameElement = "stsHttpPort";
            const string StsHttpsPortNameElement = "stsHttpsPort";

            const string V1ConfigFileName = "servicebus.config";
            const string WebRootPath = "approot\\";

            readonly string configFileName;

            bool haveSettings;
            string relayHostName;
            int relayHttpPort;
            int relayHttpsPort;
            int relayNmfPort;
            string relayPathPrefix = string.Empty; // empty by default, indicating non-onebox environment usage
            string stsHostName;
            bool stsEnabled;
            int stsHttpPort;
            int stsHttpsPort;

            public ConfigSettings()
            {
                this.configFileName = V1ConfigFileName;
                this.ReadConfigSettings();
            }

            public bool HaveSettings
            {
                get { return this.haveSettings; }
            }

            public string RelayHostRootName { get { return this.relayHostName; } }
            public int RelayHttpPort { get { return this.relayHttpPort; } }
            public int RelayHttpsPort { get { return this.relayHttpsPort; } }
            public int RelayNmfPort { get { return this.relayNmfPort; } }
            public string RelayPathPrefix { get { return this.relayPathPrefix; } }
            public string StsHostName { get { return this.stsHostName; } }
            public bool StsEnabled { get { return this.stsEnabled; } }
            public int StsHttpPort { get { return this.stsHttpPort; } }
            public int StsHttpsPort { get { return this.stsHttpsPort; } }

            void ReadConfigSettings()
            {
                this.haveSettings = false;
                string executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
                string winfabpath = null;
                if (!string.IsNullOrEmpty(executingAssemblyLocation))
                {
                    string winfabapproot = Path.GetDirectoryName(executingAssemblyLocation);
                    winfabpath = Path.Combine(winfabapproot, this.configFileName);
                }

                string serviceBusPath;
                string filepath = this.configFileName;
                string webpath = System.IO.Path.Combine(WebRootPath, this.configFileName);
                if (File.Exists(filepath))
                {
                    // config in same directory as application
                    serviceBusPath = filepath;
                }
                else if (File.Exists(webpath))
                {
                    // config in application web directory
                    serviceBusPath = webpath;
                }
                else if (!string.IsNullOrEmpty(winfabpath) && File.Exists(winfabpath))
                {
                    serviceBusPath = winfabpath;
                }
                else
                {
                    // config in same directory as machine.config
                    string machineConfigPath = ConfigurationManager.OpenMachineConfiguration().FilePath;
                    string directoryPath = Path.GetDirectoryName(machineConfigPath);
                    serviceBusPath = Path.Combine(directoryPath, this.configFileName);
                }

                // use settings from live environment by default
                LiveEnvironment defaultsettings = new LiveEnvironment();
                this.relayHostName = defaultsettings.RelayHostRootName;
                this.relayHttpPort = defaultsettings.RelayHttpPort;
                this.relayHttpsPort = defaultsettings.RelayHttpsPort;
                this.relayNmfPort = defaultsettings.RelayNmfPort;
                this.relayPathPrefix = defaultsettings.RelayPathPrefix;
                this.stsHostName = defaultsettings.StsHostName;
                this.stsEnabled = defaultsettings.StsEnabled;
                this.stsHttpPort = defaultsettings.StsHttpPort;
                this.stsHttpsPort = defaultsettings.StsHttpsPort;

                if (File.Exists(serviceBusPath))
                {
                    Stream stream = File.OpenRead(serviceBusPath);

                    XmlReader reader = XmlReader.Create(stream);

                    reader.ReadStartElement("configuration");
                    reader.ReadStartElement("Microsoft.ServiceBus");

                    while (reader.IsStartElement())
                    {
                        string elementName = reader.Name;
                        string elementContent = reader.ReadElementString();

                        switch (elementName)
                        {
                            case RelayHostNameElement:
                                this.relayHostName = elementContent;
                                break;
                            case RelayHttpPortNameElement:
                                this.relayHttpPort = int.Parse(elementContent, CultureInfo.InvariantCulture);
                                break;
                            case RelayHttpsPortNameElement:
                                this.relayHttpsPort = int.Parse(elementContent, CultureInfo.InvariantCulture);
                                break;
                            case RelayNmfPortNameElement:
                                this.relayNmfPort = int.Parse(elementContent, CultureInfo.InvariantCulture);
                                break;
                            case RelayPathPrefixElement:
                                this.relayPathPrefix = elementContent;
                                if (!this.relayPathPrefix.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                                {
                                    this.relayPathPrefix = "/" + this.relayPathPrefix;
                                }

                                if (this.relayPathPrefix.EndsWith("/", StringComparison.Ordinal))
                                {
                                    this.relayPathPrefix = this.relayPathPrefix.Substring(0, this.relayPathPrefix.Length - 1);
                                }

                                break;
                            case StsHostNameElement:
                                this.stsHostName = elementContent;
                                break;
#if DEBUG
                            case StsEnabledElement:
                                this.stsEnabled = bool.Parse(elementContent);
                                break;
#endif
                            case StsHttpPortNameElement:
                                this.stsHttpPort = int.Parse(elementContent, CultureInfo.InvariantCulture);
                                break;
                            case StsHttpsPortNameElement:
                                this.stsHttpsPort = int.Parse(elementContent, CultureInfo.InvariantCulture);
                                break;
                            default:
                                break; // skip IDM and other applications config
                        }
                    }

                    reader.ReadEndElement();    // Microsoft.ServiceBus
                    reader.ReadEndElement();    // configuration

                    stream.Close();

                    this.haveSettings = true;
                }
            }
        }
    }
}