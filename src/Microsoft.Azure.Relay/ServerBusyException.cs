//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>Represents the exception that is thrown when a server is overloaded with logical operations.</summary>
    [Serializable]
    public class ServerBusyException : RelayException
    {
        public ServerBusyException() { }

        public ServerBusyException(string message)
            : base(message) { }

        public ServerBusyException(string message, Exception inner)
            : base(message, inner) { }

        protected ServerBusyException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
