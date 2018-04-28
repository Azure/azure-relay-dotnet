// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// From https://github.com/dotnet/corefx/blob/master/src/Common/src/System/Numerics/Hashing/HashHelpers.cs
namespace System.Numerics.Hashing
{
    static class HashHelpers
    {
        public static readonly int RandomSeed = Guid.NewGuid().GetHashCode();

        public static int Combine(int h1, int h2)
        {
            unchecked
            {
                // RyuJIT optimizes this to use the ROL instruction
                // Related GitHub pull request: dotnet/coreclr#1830
                uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
                return ((int)rol5 + h1) ^ h2;
            }
        }
    }
}
