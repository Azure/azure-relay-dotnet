// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// An exception that indicates the Relay HybridConnection/Endpoint already exists.
    /// </summary>
#if SERIALIZATION
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
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public EndpointAlreadyExistsException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public EndpointAlreadyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.IsTransient = false;
        }

#if SERIALIZATION
        /// <summary>
        /// Creates a new instance of the <see cref="EndpointAlreadyExistsException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="StreamingContext" /> that contains contextual information about the source or destination. </param>
        protected EndpointAlreadyExistsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
#endif
    }
}
