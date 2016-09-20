//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class EndpointNotFoundException : RelayException
    {
        public EndpointNotFoundException() { }

        public EndpointNotFoundException(string message) : base(message) { }

        public EndpointNotFoundException(string message, Exception inner) : base(message, inner) { }

        protected EndpointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
