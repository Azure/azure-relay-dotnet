//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents exceptions thrown for for relay errors.
    /// </summary>
#if SERIALIZATION
    [Serializable]
#endif
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
        /// Creates a new instance of the <see cref="RelayException"/> class.
        /// </summary>
        public RelayException(string message) : base(message)
        {
            this.IsTransient = true;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class.
        /// </summary>
        public RelayException(string message, Exception inner) : base(message, inner)
        {
            this.IsTransient = true;
        }

#if SERIALIZATION
        /// <summary>
        /// Creates a new instance of the <see cref="RelayException"/> class.
        /// </summary>
        protected RelayException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.IsTransient = true;
        }
#endif

        /// <summary>Gets a value indicating whether the exception is transient. Check this property
        /// to determine if the operation should be retried.</summary> 
        /// <value>true if the exception is transient; otherwise, false.</value>
        public bool IsTransient { get; protected set; }
    }
}
