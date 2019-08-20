// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System.Reflection;
    using Xunit.Sdk;

    public class DisplayTestMethodNameAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            TestUtility.Log($"Begin {methodUnderTest.DeclaringType}.{methodUnderTest.Name}({TestUtility.RuntimeFramework})");
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            TestUtility.Log($"End {methodUnderTest.DeclaringType}.{methodUnderTest.Name}");
            base.After(methodUnderTest);
        }
    }
}