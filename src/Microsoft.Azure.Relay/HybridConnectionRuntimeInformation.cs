//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>Provides runtime information about a HybridConnection.</summary>
    [DataContract(Name = "HybridConnectionDescription", Namespace = RelayConstants.ManagementNamespace)]
    public class HybridConnectionRuntimeInformation
#if NET45
        : IExtensibleDataObject
#endif
    {
        [DataMember(Name = "CreatedAt", IsRequired = false, EmitDefaultValue = false, Order = 2)]
        DateTime? createdAt { get; set; }

        [DataMember(Name = "UpdatedAt", IsRequired = false, EmitDefaultValue = false, Order = 3)]
        DateTime? updatedAt { get; set; }

        [DataMember(Name = "RequiresClientAuthorization", IsRequired = false, EmitDefaultValue = false, Order = 4)]
        bool? requiresClientAuthorization { get; set; }

        [DataMember(Name = "UserMetadata", IsRequired = false, EmitDefaultValue = false, Order = 8)]
        string userMetadata { get; set; }

        [DataMember(Name = "ListenerCount", IsRequired = false, EmitDefaultValue = false, Order = 9)]
        int? listenerCount { get; set; }

        internal HybridConnectionRuntimeInformation()
        {
        }

        /// <summary>Gets the time the HybridConnection was created.</summary>
        public DateTime CreatedAt
        {
            get { return this.createdAt ?? DateTime.MinValue; }
        }

        /// <summary>Gets the time when the HybridConnection was updated.</summary>
        public DateTime UpdatedAt
        {
            get { return this.updatedAt ?? DateTime.MinValue; }
        }

        /// <summary>Gets the number of listeners for this HybridConnection.</summary>
        /// <value>The number of listeners for this HybridConnection.</value>
        public int ListenerCount
        {
            get { return this.listenerCount ?? 0; }
        }

        /// <summary>Gets a value indicating whether client authorization is needed for this HybridConnection.</summary>
        /// <value>true if client authorization is needed for this HybridConnection; otherwise, false.</value>
        public bool RequiresClientAuthorization
        {
            get { return this.requiresClientAuthorization ?? true; }
        }

        /// <summary>Gets the user metadata associated with this instance.</summary>
        /// <value>The user metadata associated with this instance.</value>
        public string UserMetadata
        {
            get { return this.userMetadata; }
        }

#if NET45
        ExtensionDataObject IExtensibleDataObject.ExtensionData { get; set; }
#endif
    }
}
