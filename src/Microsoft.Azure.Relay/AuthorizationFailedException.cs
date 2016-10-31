//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The exception that occurs when an authorization attempt fails.
    /// </summary>
    [Serializable]
    public class AuthorizationFailedException : RelayException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AuthorizationFailedException"/> class.
        /// </summary>
        public AuthorizationFailedException()
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AuthorizationFailedException"/> class.
        /// </summary>
        public AuthorizationFailedException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AuthorizationFailedException"/> class.
        /// </summary>
        public AuthorizationFailedException(string message, Exception inner)
            : base(message, inner)
        {
            this.IsTransient = false;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AuthorizationFailedException"/> class.
        /// </summary>
        protected AuthorizationFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}
