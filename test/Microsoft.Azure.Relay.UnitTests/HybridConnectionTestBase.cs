//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using Xunit.Abstractions;

    /// <summary>
    /// This class is used to ensure that inheriting classes have access to a Connection String Builder and logger.
    /// </summary>
    public abstract class HybridConnectionTestBase
    {
        private Logger logger;

        private RelayConnectionStringBuilder connectionStringBuilder;

        public HybridConnectionTestBase(ITestOutputHelper output)
        {
            this.logger = new Logger(output);

            var connectionString = Environment.GetEnvironmentVariable("RELAYCONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("RELAYCONNECTIONSTRING environment variable was not found!");
            }

            this.connectionStringBuilder = new RelayConnectionStringBuilder(connectionString)
            {
                // Unless explicitly stated, run all tests against the authenticated Hybrid Connection
                EntityPath = "authenticated",
                OperationTimeout = TimeSpan.FromSeconds(15)
            };
        }

        protected Logger Logger
        {
            get
            {
                return this.logger;
            }
        }

        protected RelayConnectionStringBuilder ConnectionStringBuilder
        {
            get
            {
                return this.connectionStringBuilder;
            }
        }
    }
}
