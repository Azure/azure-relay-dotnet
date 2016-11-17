// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using Xunit.Abstractions;

    /// <summary>
    /// This class is used to ensure that inheriting classes have access to a Connection String Builder and logger.
    /// </summary>
    public abstract class HybridConnectionTestBase
    {
        protected readonly string AuthenticatedEntityPath = "authenticated";
        protected readonly string UnauthenticatedEntityPath = "unauthenticated";

        public HybridConnectionTestBase(ITestOutputHelper output)
        {
            this.Logger = new Logger(output);

            var envConnectionString = Environment.GetEnvironmentVariable("RELAYCONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(envConnectionString))
            {
                throw new InvalidOperationException("RELAYCONNECTIONSTRING environment variable was not found!");
            }

            // Validate the connection string
            // Running most tests on the authenticated Hybrid Connection, unless already specified in the connection string

            var connectionStringBuilder = new RelayConnectionStringBuilder(envConnectionString);
            if (string.IsNullOrEmpty(connectionStringBuilder.EntityPath))
            {
                connectionStringBuilder.EntityPath = "authenticated";
            }
            this.ConnectionString = connectionStringBuilder.ToString();
        }

        protected Logger Logger { get; }

        protected string ConnectionString { get; }
    }
}
