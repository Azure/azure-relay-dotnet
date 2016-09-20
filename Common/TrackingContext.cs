//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;

    class TrackingContext
    {
        internal const string TrackingIdName = "TrackingId";
        internal const string SubsystemIdName = "SubsystemId";
        string cachedToString;

        TrackingContext(string trackingId, string subsystemId)
        {
            this.TrackingId = trackingId;
            this.SubsystemId = subsystemId;
        }

        public string TrackingId { get; private set; }

        public string SubsystemId { get; private set; }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and given subsystemId.
        /// </summary>
        /// <param name="subsystemId">subsystem-specific Uri like entity address to be used in the tracking context</param>
        internal static TrackingContext Create(Uri subsystemId)
        {
            return Create(subsystemId.GetLeftPart(UriPartial.Path));
        }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and given subsystemId.
        /// </summary>
        /// <param name="subsystemId">subsystem-specific identifier to be used in the tracking context</param>
        internal static TrackingContext Create(string subsystemId)
        {
            return Create(Guid.NewGuid().ToString(), subsystemId);
        }

        internal static TrackingContext Create(string trackingId, string subsystemId)
        {
            return new TrackingContext(trackingId, subsystemId);
        }

        internal static TrackingContext Create(string trackingId, Uri subsystemId)
        {
            return new TrackingContext(trackingId, subsystemId.GetLeftPart(UriPartial.Path));
        }

        public override string ToString()
        {
            if (this.cachedToString == null)
            {
                if (string.IsNullOrEmpty(this.SubsystemId))
                {
                    this.cachedToString = TrackingIdName + ":" + this.TrackingId;
                }
                else
                {
                    this.cachedToString = TrackingIdName + ":" + this.TrackingId + ", " + SubsystemIdName + ":" + this.SubsystemId;
                }
            }

            return this.cachedToString;
        }
    }
}
