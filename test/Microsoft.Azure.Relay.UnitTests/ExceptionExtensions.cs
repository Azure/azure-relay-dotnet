// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;

    static class ExceptionExtensions
    {
        public static string ToStringWithoutCallstack(this Exception e)
        {
            string details = e.ToString();
            return details.Split(new[] { "\r\n   at " }, 2, StringSplitOptions.None)[0];
        }
    }
}
