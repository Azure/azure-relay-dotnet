// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    static class PlatformHelpers
    {
        public static ArraySegment<byte> GetArraySegment(this MemoryStream memoryStream)
        {
            Fx.Assert(memoryStream != null, "memoryStream is required");
            
            ArraySegment<byte> buffer;
            // .NET 4.6 and .NET Core added MemoryStream.TryGetBuffer()
            // .NET Core removed MemoryStream.GetBuffer()
            if (!memoryStream.TryGetBuffer(out buffer))
            {
                buffer = new ArraySegment<byte>(memoryStream.ToArray());
            }

            return buffer;
        }

        public static string GetRuntimeFramework()
        {
            return RuntimeInformation.FrameworkDescription;
        }
    }
}
