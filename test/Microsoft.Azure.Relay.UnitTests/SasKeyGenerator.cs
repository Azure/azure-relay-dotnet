namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Security.Cryptography;

    internal static class SasKeyGenerator
    {
        internal static string GenerateRandomKey()
        {
            byte[] key256 = new byte[32];
            using (var rngCryptoServiceProvider = new RNGCryptoServiceProvider())
            {
                rngCryptoServiceProvider.GetBytes(key256);
            }

            return Convert.ToBase64String(key256);
        }
    }
}
