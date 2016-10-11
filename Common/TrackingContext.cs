//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;

    class TrackingContext
    {
        internal const string TrackingIdName = "TrackingId";
        const string SubsystemIdName = "SubsystemId";
        const string TimestampName = "Timestamp";
        string cachedToString;

        TrackingContext(string trackingId, string subsystemId)
        {
            this.TrackingId = trackingId;
            this.SubsystemId = subsystemId;
        }

        public string TrackingId { get; private set; }

        public string SubsystemId { get; private set; }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and no subsystemId.
        /// </summary>
        internal static TrackingContext Create()
        {
            return Create(Guid.NewGuid().ToString(), (string)null);
        }

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

        /// <summary>
        /// Ensures the given string contains a TrackingId. If one is already present, nothing occurs.
        /// Otherwise TrackingId, Timestamp, and if present, SystemTracker are added.
        /// </summary>
        internal string CreateTrackableErrorMessage(string exceptionMessage)
        {
            if (string.IsNullOrEmpty(exceptionMessage) || exceptionMessage.IndexOf(TrackingIdName, StringComparison.Ordinal) == -1)
            {
                // Ensure there's a period so we don't get a run-on sentence such as "An error occurred TrackingId:foo"
                if (!string.IsNullOrEmpty(exceptionMessage) && !exceptionMessage.EndsWith(".", StringComparison.Ordinal))
                {
                    exceptionMessage += ".";
                }

                return exceptionMessage + " " + this.CreateClientTrackingExceptionInfo();
            }

            return exceptionMessage;
        }

        string CreateClientTrackingExceptionInfo()
        {
            return CreateClientTrackingExceptionInfo(DateTime.UtcNow);
        }

        string CreateClientTrackingExceptionInfo(DateTime timestamp)
        {
            return string.IsNullOrWhiteSpace(this.SubsystemId) ?
                TrackingIdName + ":" + this.TrackingId + ", " + TimestampName + ":" + timestamp :
                TrackingIdName + ":" + this.TrackingId + ", " + SubsystemIdName + ":" + this.SubsystemId + ", " + TimestampName + ":" + timestamp;
        }
    }
}
