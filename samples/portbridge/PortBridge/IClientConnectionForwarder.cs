// Copyright © Microsoft Corporation
// MIT License. See LICENSE.txt for details.

namespace PortBridge
{
    public interface IClientConnectionForwarder
    {
        void Close();
        void Open();
    }
}