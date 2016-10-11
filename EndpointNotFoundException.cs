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
        public EndpointNotFoundException()
        {
            this.IsTransient = false;
        }

        public EndpointNotFoundException(string message) : base(message)
        {
            this.IsTransient = false;
        }

        public EndpointNotFoundException(string message, Exception inner) : base(message, inner)
        {
            this.IsTransient = false;
        }

        protected EndpointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}
