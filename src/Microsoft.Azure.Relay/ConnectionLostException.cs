//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

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
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class.
        /// </summary>
        public ConnectionLostException(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class.
        /// </summary>
        public ConnectionLostException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ConnectionLostException"/> class.
        /// </summary>
        protected ConnectionLostException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
