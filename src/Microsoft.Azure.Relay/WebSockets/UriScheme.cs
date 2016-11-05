namespace Microsoft.Azure.Relay.WebSockets
{
    // From: https://github.com/dotnet/corefx/blob/master/src/Common/src/System/Net/UriScheme.cs
    internal class UriScheme
    {
        public const string Http = "http";
        public const string Https = "https";
        public const string Ws = "ws";
        public const string Wss = "wss";

        public const string SchemeDelimiter = "://";
    }
}
