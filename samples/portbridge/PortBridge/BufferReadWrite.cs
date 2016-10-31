// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridge
{
    public delegate int BufferRead(byte[] buffer, int offset, int count);

    public delegate void BufferWrite(byte[] buffer, int offset, int count);
}