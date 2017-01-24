// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Diagnostics;
    using Xunit.Abstractions;

    public class Logger
    {
        readonly ITestOutputHelper output;

        public Logger(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Log(string message)
        {
            var log = string.Format("{0} {1}", DateTime.Now.TimeOfDay, message);
            this.output.WriteLine(log);
            Debug.WriteLine(message);
        }
    }
}
