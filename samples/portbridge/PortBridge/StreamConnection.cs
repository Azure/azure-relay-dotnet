// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridge
{
    using System.IO;

    public class StreamConnection
    {
        public StreamConnection(Stream stream, string connectionInfo)
        {
            Stream = stream;
            ConnectionInfo = connectionInfo;
        }

        public Stream Stream { get; }
        public string ConnectionInfo { get; }
    }
}