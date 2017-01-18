// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;

    static class TestUtility
    {
        internal static void Log(string message)
        {
            var formattedMessage = $"{DateTime.Now.TimeOfDay}: {message}";
            Debug.WriteLine(formattedMessage);
            Console.WriteLine(formattedMessage);
        }

        internal static string GenerateRandomSasKey()
        {
            var key256 = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key256);
            }

            return Convert.ToBase64String(key256);
        }
    }
}