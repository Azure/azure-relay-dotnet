namespace Microsoft.Azure.Relay.WebSockets
{
    using System.Diagnostics.Tracing;

    // From: https://github.com/dotnet/corefx/blob/master/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/NetEventSource.WebSockets.cs
    [EventSource(Name = "Microsoft-System-Net-WebSockets-Client")]
    internal sealed partial class NetEventSource { }
}
