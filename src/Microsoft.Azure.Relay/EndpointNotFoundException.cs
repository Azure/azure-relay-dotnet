// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an exception when the Relay HybridConnection/Endpoint should exist but was not present.
    /// </summary>
#if SERIALIZATION
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

#if SERIALIZATION
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
