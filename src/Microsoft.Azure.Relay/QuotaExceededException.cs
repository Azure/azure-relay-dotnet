//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class.
        /// </summary>
        public QuotaExceededException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class.
        /// </summary>
        public QuotaExceededException(string message, Exception inner)
            : base(message, inner)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuotaExceededException"/> class.
        /// </summary>
        protected QuotaExceededException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}