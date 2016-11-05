//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// An exception that indicates the Relay HybridConnection/Endpoint already exists.
    /// </summary>
#if NET45
    [Serializable]
#endif
    public class EndpointAlreadyExistsException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class.
        /// </summary>
        public EndpointAlreadyExistsException()
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class.
        /// </summary>
        public EndpointAlreadyExistsException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class.
        /// </summary>
        public EndpointAlreadyExistsException(string message, Exception inner)
            : base(message, inner)
        {
            this.IsTransient = false;
        }

#if NET45
        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class.
        /// </summary>
        protected EndpointAlreadyExistsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
#endif
    }
}
