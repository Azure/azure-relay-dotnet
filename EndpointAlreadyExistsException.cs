//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class EndpointAlreadyExistsException : RelayException
    {
        public EndpointAlreadyExistsException()
        {
            this.IsTransient = false;
        }

        public EndpointAlreadyExistsException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        public EndpointAlreadyExistsException(string message, Exception inner)
            : base(message, inner)
        {
            this.IsTransient = false;
        }

        protected EndpointAlreadyExistsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}
