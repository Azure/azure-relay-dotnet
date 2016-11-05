﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an exception when the Relay HybridConnection/Endpoint should exist but was not present.
    /// </summary>
#if NET45
    [Serializable]
#endif
    public class EndpointNotFoundException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EndpointNotFoundException"/> class.
        /// </summary>
        public EndpointNotFoundException()
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EndpointNotFoundException"/> class.
        /// </summary>
        public EndpointNotFoundException(string message) : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EndpointNotFoundException"/> class.
        /// </summary>
        public EndpointNotFoundException(string message, Exception inner) : base(message, inner)
        {
            this.IsTransient = false;
        }

#if NET45
        /// <summary>
        /// Creates a new instance of the <see cref="EndpointNotFoundException"/> class.
        /// </summary>
        protected EndpointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.IsTransient = false;
        }
#endif
    }
}
