// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents exceptions thrown for for relay errors.
    /// </summary>
    [Serializable]
    public class RelayException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class.
        /// </summary>
        public RelayException()
        {
            this.IsTransient = true;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public RelayException(string message) : base(message)
        {
            this.IsTransient = true;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RelayException(string message, Exception innerException) : base(message, innerException)
        {
            this.IsTransient = true;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="StreamingContext" /> that contains contextual information about the source or destination. </param>
        protected RelayException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.IsTransient = true;
        }

        /// <summary>Gets a value indicating whether the exception is transient. Check this property
        /// to determine if the operation should be retried.</summary> 
        /// <value>true if the exception is transient; otherwise, false.</value>
        public bool IsTransient { get; protected set; }
    }
}
