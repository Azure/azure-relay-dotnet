//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;

    static class PlatformExtensions
    {
        public static ArraySegment<byte> GetArraySegment(this MemoryStream memoryStream)
        {
            Fx.Assert(memoryStream != null, "memoryStream is required");
            
            ArraySegment<byte> buffer;
#if NET45
            buffer = new ArraySegment<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
#else
            // .NET 4.6 and .NET Core added MemoryStream.TryGetBuffer()
            // .NET Core removed MemoryStream.GetBuffer()
            if (!memoryStream.TryGetBuffer(out buffer))
            {
                buffer = new ArraySegment<byte>(memoryStream.ToArray());
            }
#endif

            return buffer;
        }
    }
}
