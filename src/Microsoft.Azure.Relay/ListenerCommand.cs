// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    /// <summary>
    /// DataContract representation of HybridConnection Listener JSON Commands
    /// <para/>
    /// Accept:
    /// {
    ///   "accept" : {
    ///     "address" : "wss://g63-prod-xxx-001-sb.servicebus.windows.net:443/$hc/YourHybridConnection?sb-hc-action=accept&amp;sb-hc-id=GUID_G0_G1",
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

        [DataMember(Name = "accept", EmitDefaultValue = false, IsRequired = false)]
        public AcceptCommand Accept { get; set; }

        [DataMember(Name = "renewToken", EmitDefaultValue = false, IsRequired = false)]
        public RenewTokenCommand RenewToken { get; set; }

        [DataMember(Name = "request", EmitDefaultValue = false, IsRequired = false)]
        public RequestCommand Request { get; set; }

        [DataMember(Name = "response", EmitDefaultValue = false, IsRequired = false)]
        public ResponseCommand Response { get; set; }

#if DEBUG
        [DataMember(Name = "injectFault", EmitDefaultValue = false, IsRequired = false)]
        public InjectFaultCommand InjectFault { get; set; }
#endif

        public static ListenerCommand ReadObject(ArraySegment<byte> buffer)
        {
            using (var stream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count))
            {
                return ListenerCommand.ReadObject(stream);
            }
        }

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
        ///     "address" : "wss://g63-prod-xxx-001-sb.servicebus.windows.net:443/$hc/YourHybridConnection?sb-hc-action=accept&amp;sb-hc-id=GUID_G0_G1",
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

            [DataMember(Name = "remoteEndpoint", Order = 3, EmitDefaultValue = false, IsRequired = false)]
            Endpoint remoteEndpoint;

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

            [IgnoreDataMember]
            public Endpoint RemoteEndpoint
            {
                get
                {
                    if (this.remoteEndpoint == null)
                    {
                        this.remoteEndpoint = new Endpoint();
                    }

                    return this.remoteEndpoint;
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

        /// <summary>
        /// DataContract for JSON such as the following:
        /// <para/>
        /// {
        ///   "request" : {
        ///     "address" : "wss://dc-node.servicebus.windows.net:443/$hc/{path}?sb-hc-action=request&amp;...",
        ///     "id" : "42c34cb5-7a04-4d40-a19f-bdc66441e736_G10",
        ///     "requestTarget" : "/abc/def?myarg=value&amp;otherarg=...",
        ///     "method" : "GET",
        ///     "remoteEndpoint" : {
        ///       "address" : "10.0.0.1",
        ///       "port" : 1234,
        ///     },
        ///     "requestHeaders" : {
        ///       "Host": "contoso.servicebus.windows.net"
        ///       "Content-Type" : "...",
        ///       "User-Agent" : "...",
        ///     },
        ///     "body" : true
        ///   }
        /// }
        /// </summary>
        [DataContract]
        public class RequestCommand
        {
            [DataMember(Name = "address", Order = 0, EmitDefaultValue = false, IsRequired = false)]
            public string Address { get; set; }

            [DataMember(Name = "id", Order = 1, EmitDefaultValue = false, IsRequired = false)]
            public string Id { get; set; }

            [DataMember(Name = "requestTarget", Order = 2, EmitDefaultValue = false, IsRequired = false)]
            public string RequestTarget { get; set; }

            [DataMember(Name = "method", Order = 3, EmitDefaultValue = false, IsRequired = false)]
            public string Method { get; set; }

            [DataMember(Name = "remoteEndpoint", Order = 4, EmitDefaultValue = false, IsRequired = false)]
            Endpoint remoteEndpoint;

            [DataMember(Name = "requestHeaders", Order = 5, EmitDefaultValue = false, IsRequired = false)]
            IDictionary<string, string> requestHeaders;

            [DataMember(Name = "body", Order = 6, EmitDefaultValue = false, IsRequired = false)]
            public bool? Body { get; set; }

            [IgnoreDataMember]
            public Endpoint RemoteEndpoint
            {
                get
                {
                    if (this.remoteEndpoint == null)
                    {
                        this.remoteEndpoint = new Endpoint();
                    }

                    return this.remoteEndpoint;
                }
            }

            [IgnoreDataMember]
            public IDictionary<string, string> RequestHeaders
            {
                get
                {
                    if (this.requestHeaders == null)
                    {
                        this.requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    return this.requestHeaders;
                }
            }
        }

        /// <summary>
        /// DataContract for JSON such as the following:
        /// <para/>
        /// {
        ///   "response" : {
        ///   "requestId" : "42c34cb5-7a04-4d40-a19f-bdc66441e736",
        ///   "statusCode" : "200",
        ///   "statusDescription" : "OK",
        ///   "responseHeaders" : {
        ///     "Content-Type" : "application/json",
        ///     "Content-Encoding" : "gzip"
        ///   }
        ///   "body" : true
        /// }
        /// </summary>
        [DataContract]
        public class ResponseCommand
        {
            [DataMember(Name = "requestId", IsRequired = true)]
            public string RequestId { get; set; }

            [DataMember(Name = "statusCode", IsRequired = true)]
            public int StatusCode { get; set; }

            [DataMember(Name = "statusDescription", EmitDefaultValue = false, IsRequired = false)]
            public string StatusDescription { get; set; }

            [DataMember(Name = "responseHeaders", EmitDefaultValue = false, IsRequired = false)]
            IDictionary<string, string> responseHeaders;

            [DataMember(Name = "body", IsRequired = true)]
            public bool Body { get; set; }

            [IgnoreDataMember]
            public IDictionary<string, string> ResponseHeaders
            {
                get
                {
                    if (this.responseHeaders == null)
                    {
                        this.responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    return this.responseHeaders;
                }
            }
        }

        /// <summary>
        /// DataContract for JSON such as the following:
        /// <para/>
        /// {
        ///   "endpoint" : {
        ///     "address" : "10.0.0.1",
        ///     "port" : 1234
        ///   }
        /// }
        /// </summary>
        [DataContract]
        public class Endpoint
        {
            [DataMember(Name = "address", EmitDefaultValue = false, IsRequired = false)]
            public string Address { get; set; }

            [DataMember(Name = "port", EmitDefaultValue = false, IsRequired = false)]
            public int Port { get; set; }
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