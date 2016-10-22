//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;

    static class ExceptionExtensions
    {
        public const string SuppressStackTraceOnRethrowKey = "SuppressStackTraceOnRethrow";

        /// <summary>
        /// Rethrow the Exception while preserving the previous throw location. The following string is inserted in the
        /// stack trace to indicate the restore point: "End of stack trace from the previous location where the 
        /// exception was thrown". This is similar to the way inner exceptions or marshaled exceptions are indicated in stack traces.
        /// <para />This method always throws an exception.  It has a return value in order to write calling code like below
        /// so that the compiler also knows the code doesn't continue after this call:
        /// <para/>
        /// throw exception.Rethrow();
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public static Exception Rethrow(this Exception exception)
        {
            Fx.Assert(exception != null, "The specified Exception is null.");

            if (exception.Data != null && exception.Data.Contains(SuppressStackTraceOnRethrowKey))
            {
                // SuppressStackTraceOnRethrow is so certain exceptions that get thrown a number of times don't get a super long stack trace.
                // NOTE: ThrowException is a helper/wrapper method that simply calls "throw exception;".  This wrapper method exists so that 
                // Rethrow will indeed get inlined (having a throw in this method causes it to not be inlined any more).
                ThrowException(exception);
            }
            else
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
            return exception;
        }

        public static Exception SuppressStackTraceOnRethrow(this Exception exception)
        {
            exception.Data[SuppressStackTraceOnRethrowKey] = string.Empty;
            return exception;
        }

        /// <summary>
        /// ThrowException is a helper/wrapper method that simply calls "throw exception;".  This wrapper method exists so that 
        /// the calling method (Rethrow) will indeed get inlined (having a direct throw in Rethrow causes it to not be inlined any more).
        /// </summary>
        [DebuggerHidden]
        static void ThrowException(Exception exception)
        {
            throw exception;
        }
    }
}