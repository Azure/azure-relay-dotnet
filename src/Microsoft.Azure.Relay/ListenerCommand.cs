// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    /// <summary>
    /// DataContract representation of HybridConnection Listener JSON Commands
    /// <para/>
    /// Accept:
    /// {
    ///   "accept" : {
    ///     "address" : "wss:\/\/168.61.148.205:443\/$servicebus\/hybridconnection?action=accept&amp;path=somehybridconnection&amp;id=GUID_G0_G1",
    ///     "id" : "GUID_G0_G1",
    ///     "connectHeaders" : {
    ///       "Sec-WebSocket-Key": "l8fWd829IrZbEqUcLejv+Q==",
    ///       "Sec-WebSocket-Version": "13",
    ///       "Sec-WebSocket-Protocol": "wssubprotocol",
    ///       "Connection": "Upgrade",
    ///       "Upgrade": "websocket",
    ///       "Host": "contoso.servicebus.windows.net:443"
    ///     }
    ///   }
    /// }
    /// <para/>
    /// RenewToken:
    /// {
    ///   "renewToken" : {
    ///     "token" : "SharedAccessSignature sr=http%3a%2f%2fcontoso.servicebus.windows.net%2fHybridConnection1%2f&amp;sig=XXXXXXXXXX%3d&amp;se=1471633754&amp;skn=SasKeyName"
    ///   }
    /// }
    /// </summary>
    [DataContract]
    class ListenerCommand
    {
        [IgnoreDataMember]
        static readonly DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ListenerCommand), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        [DataMember(Name = "accept", EmitDefaultValue = false)]
        public AcceptCommand Accept { get; set; }

        [DataMember(Name = "renewToken", EmitDefaultValue = false)]
        public RenewTokenCommand RenewToken { get; set; }

#if DEBUG
        [DataMember(Name = "injectFault", EmitDefaultValue = false)]
        public InjectFaultCommand InjectFault { get; set; }
#endif

        public static ListenerCommand ReadObject(Stream stream)
        {
            return (ListenerCommand)serializer.ReadObject(stream);
        }

        public void WriteObject(Stream stream)
        {
            serializer.WriteObject(stream, this);
        }

        /// <summary>
        /// DataContract for JSON such as the following:
        /// <para/>
        /// {
        ///   "accept" : {
        ///     "address" : "wss:\/\/168.61.148.205:443\/$servicebus\/hybridconnection?action=accept&amp;path=somehybridconnection&amp;id=GUID_G0_G1",
        ///     "id" : "GUID_G0_G1",
        ///     "connectHeaders" : {
        ///       "Sec-WebSocket-Key": "l8fWd829IrZbEqUcLejv+Q==",
        ///       "Sec-WebSocket-Version": "13",
        ///       "Sec-WebSocket-Protocol": "wssubprotocol",
        ///       "Connection": "Upgrade",
        ///       "Upgrade": "websocket",
        ///       "Host": "contoso.servicebus.windows.net:443"
        ///     }
        ///   }
        /// }
        /// </summary>
        [DataContract]
        public class AcceptCommand
        {
            [DataMember(Name = "address", Order = 0)]
            public string Address { get; set; }

            [DataMember(Name = "id", Order = 1)]
            public string Id { get; set; }

            [DataMember(Name = "connectHeaders", Order = 2)]
            IDictionary<string, string> connectHeaders;

            [IgnoreDataMember]
            public IDictionary<string, string> ConnectHeaders
            {
                get
                {
                    if (this.connectHeaders == null)
                    {
                        this.connectHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    return this.connectHeaders;
                }
            }
        }

        /// <summary>
        /// DataContract for JSON such as the following:
        /// {
        ///   "renewToken" : {
        ///     "token" : "SharedAccessSignature sr=http%3a%2f%2fcontoso.servicebus.windows.net%2fHybridConnection1%2f&amp;sig=XXXXXXXXXX%3d&amp;se=1471633754&amp;skn=SasKeyName"
        ///   }
        /// }
        /// </summary>
        [DataContract]
        public class RenewTokenCommand
        {
            [DataMember(Name = "token", Order = 0)]
            public string Token { get; set; }
        }

#if DEBUG
        /// <summary>
        /// DataContract for JSON such as the following:
        /// {
        ///   "injectFault" : {
        ///     "delay" : "00:01:00"
        ///   }
        /// }
        /// </summary>
        [DataContract]
        public class InjectFaultCommand
        {
            [DataMember(Name = "delay", Order = 0)]
            public TimeSpan Delay { get; set; }
        }
#endif // DEBUG
    }
}