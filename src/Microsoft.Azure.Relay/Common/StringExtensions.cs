//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

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
