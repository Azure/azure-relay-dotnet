// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Globalization;

    static class StringExtensions
    {
        /// <summary>
        /// Ensures the given string ends with the requested pattern.  If it does no allocations are performed.
        /// </summary>
        public static string EnsureEndsWith(this string str, string value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            if (str.EndsWith(value, comparisonType))
            {
                return str;
            }

            return str + value;
        }

        public static string FormatInvariant(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }
}
