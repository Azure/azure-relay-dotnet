// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System.Threading.Tasks;

    interface ICloseAsync
    {
        Task CloseAsync();
    }
}