// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Relay;
    using Xunit;
    using Xunit.Abstractions;

    public class ConnectionStringBuilderTests
    {
        readonly Uri endpoint;
        readonly string sasKeyName;
        readonly string entityPath;
        readonly string sasKeyValue;

        public ConnectionStringBuilderTests(ITestOutputHelper output)
        {
            this.endpoint = new Uri("sb://contoso.servicebus.windows.net/");
            this.sasKeyName = "RootManageSharedAccessKey";
            this.entityPath = "hc1";
            this.sasKeyValue = TestUtility.GenerateRandomSasKey();
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task ConnectionStringBuilderOperationValidation()
        {
            TestUtility.Log("Create a new connection string using RelayConnectionStringBuilder properties.");
            var connectionStringBuilder = new RelayConnectionStringBuilder()
            {
                Endpoint = this.endpoint,
                EntityPath = this.entityPath,
                SharedAccessKeyName = this.sasKeyName,
                SharedAccessKey = this.sasKeyValue
            };
            var connectionString = connectionStringBuilder.ToString();

            // Endpoint is expected to appear first in the connection string.
            Assert.StartsWith("Endpoint=", connectionString);

            Assert.Contains($"Endpoint={this.endpoint.AbsoluteUri}", connectionString);
            Assert.Contains($"EntityPath={this.entityPath}", connectionString);
            Assert.Contains($"SharedAccessKeyName={this.sasKeyName}", connectionString);
            Assert.Contains($"SharedAccessKey={this.sasKeyValue}", connectionString);

            // OperationTimeout should be omitted since it is the default value.
            Assert.DoesNotContain("OperationTimeout", connectionString);

            // SharedAccessSignature should be omitted since it is not specified.
            Assert.DoesNotContain("SharedAccessSignature", connectionString);

            TestUtility.Log("Try to set the timeout to a negative number");
            Assert.Throws<ArgumentOutOfRangeException>(() => connectionStringBuilder.OperationTimeout = TimeSpan.FromMinutes(-1));

            TestUtility.Log("Try to create a connection string with SAS KeyName but no KeyValue");
            connectionStringBuilder.SharedAccessKeyName = this.sasKeyName;
            connectionStringBuilder.SharedAccessKey = null;
            Assert.Throws<ArgumentException>(() => connectionStringBuilder.ToString());

            TestUtility.Log("Try to create a connection string with SAS KeyValue but no KeyName");
            connectionStringBuilder.SharedAccessKeyName = null;
            connectionStringBuilder.SharedAccessKey = this.sasKeyValue;
            Assert.Throws<ArgumentException>(() => connectionStringBuilder.ToString());

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(this.sasKeyName, this.sasKeyValue);
            var sasToken = await tokenProvider.GetTokenAsync(this.endpoint.AbsoluteUri, TimeSpan.FromMinutes(5));

            TestUtility.Log("Create a connection string with SAS token only");
            connectionStringBuilder.SharedAccessSignature = sasToken.TokenString;
            connectionStringBuilder.SharedAccessKeyName = null;
            connectionStringBuilder.SharedAccessKey = null;
            connectionString = connectionStringBuilder.ToString();

            TestUtility.Log("Parse a connection string with SAS token only");
            connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
            Assert.Equal(sasToken.TokenString, connectionStringBuilder.SharedAccessSignature);
            Assert.Null(connectionStringBuilder.SharedAccessKeyName);
            Assert.Null(connectionStringBuilder.SharedAccessKey);

            TestUtility.Log("Create a connection string with SAS token and SAS KeyName");
            connectionStringBuilder.SharedAccessSignature = sasToken.TokenString;
            connectionStringBuilder.SharedAccessKeyName = this.sasKeyName;
            connectionStringBuilder.SharedAccessKey = null;
            Assert.Throws<ArgumentException>(() => connectionStringBuilder.ToString());

            TestUtility.Log("Create a connection string with SAS token and SAS Key value");
            connectionStringBuilder.SharedAccessSignature = sasToken.TokenString;
            connectionStringBuilder.SharedAccessKeyName = null;
            connectionStringBuilder.SharedAccessKey = this.sasKeyValue;
            Assert.Throws<ArgumentException>(() => connectionStringBuilder.ToString());

            TestUtility.Log("Create a new ConnectionStringBuilder, set no properties, and call ToString()");
            connectionStringBuilder = new RelayConnectionStringBuilder();
            Assert.Throws<ArgumentNullException>(() => connectionStringBuilder.ToString());

            TestUtility.Log("Set only Endpoint, call ToString()");
            connectionStringBuilder = new RelayConnectionStringBuilder()
            {
                Endpoint = this.endpoint
            };
            connectionString = connectionStringBuilder.ToString();

            TestUtility.Log("Set OperationTimeout using connectionString");
            TimeSpan operationTimeout = TimeSpan.FromSeconds(90);
            connectionString += $"OperationTimeout={operationTimeout.ToString(null, CultureInfo.InvariantCulture)}";
            connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
            Assert.Equal(operationTimeout, connectionStringBuilder.OperationTimeout);
            connectionString = connectionStringBuilder.ToString();

            var expectedOperationTimeOutString = $";OperationTimeout={operationTimeout.ToString(null, CultureInfo.InvariantCulture)}";
            Assert.Contains(expectedOperationTimeOutString, connectionString);
        }

        [Fact]
        [DisplayTestMethodName]
        public void CreateConnectionStringBuilderFromConnectionString()
        {
            var connectionStringBuilder = new RelayConnectionStringBuilder()
            {
                Endpoint = this.endpoint,
                EntityPath = this.entityPath,
                SharedAccessKeyName = this.sasKeyName,
                SharedAccessKey = this.sasKeyValue
            };
            var connectionString = connectionStringBuilder.ToString();

            TestUtility.Log("Use ConnectionStringBuilder..ctor(string) to parse the created connectionString");
            connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
            Assert.Equal(this.endpoint, connectionStringBuilder.Endpoint);
            Assert.Equal(this.entityPath, connectionStringBuilder.EntityPath);
            Assert.Equal(this.sasKeyName, connectionStringBuilder.SharedAccessKeyName);
            Assert.Equal(this.sasKeyValue, connectionStringBuilder.SharedAccessKey);
            Assert.Equal(TimeSpan.FromMinutes(1), connectionStringBuilder.OperationTimeout);
            Assert.Null(connectionStringBuilder.SharedAccessSignature);
        }
    }
}