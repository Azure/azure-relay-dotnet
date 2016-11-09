//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
        /// Creates a new instance of the <see cref="ServerBusyException"/> class.
        /// </summary>
        public ServerBusyException(string message)
            : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class.
        /// </summary>
        public ServerBusyException(string message, Exception inner)
            : base(message, inner) { }

#if SERIALIZATION
        /// <summary>
        /// Creates a new instance of the <see cref="ServerBusyException"/> class.
        /// </summary>
        protected ServerBusyException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
#endif
    }
}
