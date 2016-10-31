// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridge
{
    using System;
    using System.Threading;

    public class ThrottledQueueBufferedStream : QueueBufferedStream
    {
        readonly Semaphore sempahore;

        public ThrottledQueueBufferedStream(int throttleCapacity)
        {
            sempahore = new Semaphore(throttleCapacity, throttleCapacity);
        }

        public ThrottledQueueBufferedStream(TimeSpan naglingDelay)
            : base(naglingDelay)
        {
        }

        protected override void EnqueueChunk(byte[] chunk)
        {
            sempahore.WaitOne();
            DataChunksQueue.EnqueueAndDispatch(chunk, ChunkDequeued);
        }

        void ChunkDequeued()
        {
            sempahore.Release();
        }
    }
}