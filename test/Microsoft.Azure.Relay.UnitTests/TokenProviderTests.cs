//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class TokenProviderTests
    {
        private Logger logger;

        public TokenProviderTests(ITestOutputHelper output)
        {
            this.logger = new Logger(output);
        }

        [Fact]
        public async Task ParameterValidation()
        {
            this.logger.Log("Testing TokenProvider parameter validation");

            var keyValue = SasKeyGenerator.GenerateRandomKey();

            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider(null));
            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider(string.Empty));
            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider(null, keyValue));
            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider(string.Empty, keyValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider(new string('n', 257), keyValue));
            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", null));
            Assert.Throws<ArgumentNullException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => TokenProvider.CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", new string('v', 257)));

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", keyValue);

            await Assert.ThrowsAsync<ArgumentNullException>(() => tokenProvider.GetTokenAsync(null, TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<ArgumentNullException>(() => tokenProvider.GetTokenAsync(string.Empty, TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tokenProvider.GetTokenAsync("http://contoso.servicebus.windows.net/BadTimeout", TimeSpan.FromSeconds(-1)));
        }
    }
}