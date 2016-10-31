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
        public RelayException()
        {
            this.IsTransient = true;
        }

        public RelayException(string message) : base(message)
        {
            this.IsTransient = true;
        }

        public RelayException(string message, Exception inner) : base(message, inner)
        {
            this.IsTransient = true;
        }

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
