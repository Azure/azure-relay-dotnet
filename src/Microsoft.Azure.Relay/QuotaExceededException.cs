// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// An exception indicating a Relay quota has been exceeded.
    /// </summary>
    [Serializable]
    public class QuotaExceededException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class.
        /// </summary>
        public QuotaExceededException()
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public QuotaExceededException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public QuotaExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> that holds the serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="StreamingContext" /> that contains contextual information about the source or destination. </param>
        protected QuotaExceededException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}