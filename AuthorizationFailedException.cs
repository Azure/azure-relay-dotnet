//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>The exception that occurs when an authorization attempt fails. </summary>
    [Serializable]
    public class AuthorizationFailedException : RelayException
    {
        public AuthorizationFailedException() { }

        public AuthorizationFailedException(string message)
            : base(message) { }

        public AuthorizationFailedException(string message, Exception inner)
            : base(message, inner) { }

        protected AuthorizationFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
