//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class RelayException : Exception
    {
        public RelayException() { }

        public RelayException(string message) : base(message) { }

        public RelayException(string message, Exception inner) : base(message, inner) { }

        protected RelayException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
