// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets
{
    using System.Diagnostics.Tracing;

    // From: https://github.com/dotnet/corefx/blob/master/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/NetEventSource.WebSockets.cs
    [EventSource(Name = "Microsoft-System-Net-WebSockets-Client")]
    internal sealed partial class NetEventSource { }
}