//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class ParameterValidationTests : HybridConnectionTestBase
    {
        public ParameterValidationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TokenProviderValidation()
        {
            this.Logger.Log("Testing TokenProvider parameter validation");

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

        [Fact]
        public void RelayConnectionStringBuilderValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayConnectionStringBuilder(null));
            Assert.Throws<ArgumentNullException>(() => new RelayConnectionStringBuilder(string.Empty));

            Assert.Throws<ArgumentNullException>(() => new RelayConnectionStringBuilder { Endpoint = null });
            Assert.Throws<ArgumentNullException>(() => new RelayConnectionStringBuilder().ToString());
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder { Endpoint = new Uri("/NotAbsoluteUri", UriKind.Relative) });
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("Endpoint=sb://contoso.servicebus.windows.net/;OperationTimeout=NotATimeSpan"));

            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("Endpoint=NOT_A_URI"));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("Endpoint="));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("OperationTimeout="));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("NOT_A_KEY=value"));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("=NO_KEY"));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("NOT_A_KEY_VALUE_PAIR"));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder(" "));
            Assert.Throws<ArgumentException>(() => new RelayConnectionStringBuilder("; "));

            this.Logger.Log("Try some other weird combinations which shouldn't fail.");

            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net/").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net".ToLowerInvariant()).ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net:443").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net:443;").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net:443;;").ToString();
            new RelayConnectionStringBuilder(";Endpoint=sb://whatever.servicebus.windows.net:443").ToString();
            new RelayConnectionStringBuilder(";;Endpoint=sb://whatever.servicebus.windows.net:443").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net:443;;EntityPath=foo").ToString();
            new RelayConnectionStringBuilder("Endpoint=sb://whatever.servicebus.windows.net:443/;;EntityPath=foo;").ToString();
        }

        [Fact]
        public void ClientValidation()
        {
            string connectionString = "Endpoint=sb://whatever.servicebus.windows.net/";
            string connectionStringWithEntityPath = "Endpoint=sb://whatever.servicebus.windows.net/;EntityPath=" + this.ConnectionStringBuilder.EntityPath;
            string connectionStringWithSASKeyValueOnly = "Endpoint=sb://whatever.servicebus.windows.net/;SharedAccessKey=" + this.ConnectionStringBuilder.SharedAccessKey;

            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient((string)null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionString));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(connectionString, null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(connectionString, string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionStringWithEntityPath, this.ConnectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionStringWithSASKeyValueOnly, this.ConnectionStringBuilder.EntityPath));
        }

        [Fact]
        public void ListenerValidation()
        {
            string connectionString = "Endpoint=sb://whatever.servicebus.windows.net/";
            string connectionStringWithEntityPath = "Endpoint=sb://whatever.servicebus.windows.net/;EntityPath=" + this.ConnectionStringBuilder.EntityPath;
            string connectionStringWithSASKeyValueOnly = "Endpoint=sb://whatever.servicebus.windows.net/;SharedAccessKey=" + this.ConnectionStringBuilder.SharedAccessKey;

            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener((string)null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionString));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(connectionString, null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(connectionString, string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionString, this.ConnectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionStringWithEntityPath, this.ConnectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionStringWithSASKeyValueOnly, this.ConnectionStringBuilder.EntityPath));
        }
    }
}