// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>Represents the exception that is thrown when a server is overloaded with logical operations.</summary>
#if SERIALIZATION
    [Serializable]
#endif
    public class ServerBusyException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class.
        /// </summary>
        public ServerBusyException() { }

        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ServerBusyException(string message)
            : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ServerBusyException(string message, Exception innerException)
            : base(message, innerException) { }

#if SERIALIZATION
        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="StreamingContext" /> that contains contextual information about the source or destination. </param>
        protected ServerBusyException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
#endif
    }
}
