// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Runtime.InteropServices;

    // From: https://referencesource.microsoft.com/#System/net/System/Net/WebSockets/WebSocketBuffer.cs
    // This class helps to abstract the internal WebSocket buffer, which is used to interact with the native WebSocket
    // protocol component (WSPC). It helps to shield the details of the layout and the involved pointer arithmetic.
    // The internal WebSocket buffer also contains a segment, which is used by the WebSocketBase class to buffer 
    // payload (parsed by WSPC already) for the application, if the application requested fewer bytes than the
    // WSPC returned. The internal buffer is pinned for the whole lifetime if this class.
    // LAYOUT:
    // | Native buffer              | PayloadReceiveBuffer | PropertyBuffer |
    // | RBS + SBS + 144            | RBS                  | PBS            |
    // | Only WSPC may modify       | Only WebSocketBase may modify         | 
    //
    // *RBS = ReceiveBufferSize, *SBS = SendBufferSize
    // *PBS = PropertyBufferSize (32-bit: 16, 64 bit: 20 bytes)
    internal class WebSocketBuffer
    {
        private const int NativeOverheadBufferSize = 144;
        internal const int MinSendBufferSize = 16;
        internal const int MinReceiveBufferSize = 256;
        internal const int MaxBufferSize = 64 * 1024;
        private static readonly int s_SizeOfUInt = Marshal.SizeOf(typeof(uint));
        private static readonly int s_SizeOfBool = Marshal.SizeOf(typeof(bool));
        private static readonly int s_PropertyBufferSize = 2 * s_SizeOfUInt + s_SizeOfBool + IntPtr.Size;

        private static int GetNativeSendBufferSize(int sendBufferSize, bool isServerBuffer)
        {
            return isServerBuffer ? MinSendBufferSize : sendBufferSize;
        }

        internal static void Validate(int count, int receiveBufferSize, int sendBufferSize, bool isServerBuffer)
        {
            Contract.Assert(receiveBufferSize >= MinReceiveBufferSize,
                "'receiveBufferSize' MUST be at least " + MinReceiveBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");
            Contract.Assert(sendBufferSize >= MinSendBufferSize,
                "'sendBufferSize' MUST be at least " + MinSendBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");

            int minBufferSize = GetInternalBufferSize(receiveBufferSize, sendBufferSize, isServerBuffer);
            if (count < minBufferSize)
            {
                throw new ArgumentOutOfRangeException("internalBuffer",
                    SR.GetString(SR.net_WebSockets_ArgumentOutOfRange_InternalBuffer, minBufferSize));
            }
        }

        private static int GetInternalBufferSize(int receiveBufferSize, int sendBufferSize, bool isServerBuffer)
        {
            Contract.Assert(receiveBufferSize >= MinReceiveBufferSize,
                "'receiveBufferSize' MUST be at least " + MinReceiveBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");
            Contract.Assert(sendBufferSize >= MinSendBufferSize,
                "'sendBufferSize' MUST be at least " + MinSendBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");

            Contract.Assert(receiveBufferSize <= MaxBufferSize,
                "'receiveBufferSize' MUST be less than or equal to " + MaxBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");
            Contract.Assert(sendBufferSize <= MaxBufferSize,
                "'sendBufferSize' MUST be at less than or equal to " + MaxBufferSize.ToString(NumberFormatInfo.InvariantInfo) + ".");

            int nativeSendBufferSize = GetNativeSendBufferSize(sendBufferSize, isServerBuffer);
            return 2 * receiveBufferSize + nativeSendBufferSize + NativeOverheadBufferSize + s_PropertyBufferSize;
        }
    }
}
