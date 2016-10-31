//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class ConnectionLostException : RelayException
    {
        public ConnectionLostException() { }

        public ConnectionLostException(string message) : base(message) { }

        public ConnectionLostException(string message, Exception inner) : base(message, inner) { }

        protected ConnectionLostException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
