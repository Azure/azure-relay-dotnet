// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// An exception that occurs when a Listener gets disconnected from the Azure cloud service.
    /// </summary>
    [Serializable]
    public class ConnectionLostException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class.
        /// </summary>
        public ConnectionLostException() { }

        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ConnectionLostException(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ConnectionLostException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="StreamingContext" /> that contains contextual information about the source or destination. </param>
        protected ConnectionLostException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
