//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class QuotaExceededException : RelayException
    {
        public QuotaExceededException()
        {
            this.IsTransient = false;
        }

        public QuotaExceededException(string message)
            : base(message)
        {
            this.IsTransient = false;
        }

        public QuotaExceededException(string message, Exception inner)
            : base(message, inner)
        {
            this.IsTransient = false;
        }

        protected QuotaExceededException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.IsTransient = false;
        }
    }
}