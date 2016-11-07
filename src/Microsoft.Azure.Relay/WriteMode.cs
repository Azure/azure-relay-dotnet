namespace Microsoft.Azure.Relay
{
    /// <summary>
    /// WriteMode options for HybridConnectionStream
    /// </summary>
    public enum WriteMode
    {
        /// <summary>
        /// Write Binary frames on the WebSocket
        /// </summary>
        Binary,
        /// <summary>
        /// Write Text Frames on the WebSocket
        /// </summary>
        Text
    }
}