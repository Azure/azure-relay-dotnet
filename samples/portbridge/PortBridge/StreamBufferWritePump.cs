// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridge
{
    using System.IO;

    public class StreamBufferWritePump : BufferPump
    {
        Stream stream;

        public StreamBufferWritePump(Stream stream, BufferWrite bufferWrite)
            : base(stream.Read, bufferWrite)
        {
            this.stream = stream;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }
    }
}