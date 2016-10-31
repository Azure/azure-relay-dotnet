//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;

    /// <summary>Describes the current status of a one-way connection.</summary>
    public interface IConnectionStatus
    {
        /// <summary>Occurs when the connection is being established.</summary>
        event EventHandler Connecting;

        /// <summary>Occurs when the connection goes offline.</summary>
        event EventHandler Offline;

        /// <summary>Occurs when the connection comes online.</summary>
        event EventHandler Online;

        /// <summary>Gets a value that determines whether the connection is online.</summary>
        /// <value>true if the connection is alive and online; false if there 
        /// is no connectivity towards the Azure Service Bus from the current network location.</value> 
        bool IsOnline { get; }

        /// <summary>Retrieves the last error encountered when trying to reestablish the connection from the offline state.</summary>
        /// <value>Returns a <see cref="System.Exception" /> that contains the last error.</value>
        Exception LastError { get; }
    }
}
