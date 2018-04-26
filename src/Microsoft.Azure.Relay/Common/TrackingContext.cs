// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;

    class TrackingContext
    {
        static readonly int GuidStringLength = Guid.Empty.ToString().Length;
        internal const string TrackingIdName = "TrackingId";
        const string SubsystemIdName = "SubsystemId";
        const string TimestampName = "Timestamp";
        string cachedToString;

        TrackingContext(Guid activityId, string trackingId, string subsystemId)
        {
            this.ActivityId = activityId;
            this.TrackingId = trackingId;
            this.SubsystemId = subsystemId;
        }

        public Guid ActivityId { get; }

        public string TrackingId { get; }

        public string SubsystemId { get; }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and no subsystemId.
        /// </summary>
        internal static TrackingContext Create()
        {
            return Create(Guid.NewGuid(), (string)null);
        }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and given subsystemId.
        /// </summary>
        /// <param name="subsystemId">subsystem-specific Uri like entity address to be used in the tracking context</param>
        internal static TrackingContext Create(Uri subsystemId)
        {
            return Create(Guid.NewGuid(), subsystemId.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(Guid activityId, string subsystemId)
        {
            return Create(activityId, activityId.ToString(), subsystemId);
        }

        internal static TrackingContext Create(string trackingId, Uri subsystemId)
        {
            return Create(trackingId, subsystemId.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(string trackingId, string subsystemId)
        {
            Guid activityId;
            bool parseFailed = false;
            if (!Guid.TryParse(trackingId.Substring(0, Math.Min(GuidStringLength, trackingId.Length)), out activityId))
            {
                parseFailed = true;
                activityId = Guid.NewGuid();
            }

            var trackingContext = Create(activityId, trackingId, subsystemId);
            if (parseFailed)
            {
                RelayEventSource.Log.Info(nameof(TrackingContext), trackingContext, $"Parsing TrackingId:'{trackingId}' as Guid failed, created new ActivityId:{activityId} for trace correlation.");
            }

            return trackingContext;
        }

        internal static TrackingContext Create(Guid activityId, Uri subsystemId)
        {
            return Create(activityId, activityId.ToString(), subsystemId);
        }

        internal static TrackingContext Create(Guid activityId, string trackingId, Uri subsystemId)
        {
            return Create(activityId, trackingId, subsystemId.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(Guid activityId, string trackingId, string subsystemId)
        {
            return new TrackingContext(activityId, trackingId, subsystemId);
        }

        /// <summary>
        /// Given a trackingId string with "_GXX" suffix remove that suffix.
        /// Example: "1c048eb5-77c4-4b85-96fd-fa526801af35_G0" becomes "1c048eb5-77c4-4b85-96fd-fa526801af35"
        /// </summary>
        internal static string RemoveSuffix(string trackingId)
        {
            int roleSuffixIndex = trackingId.IndexOf("_");
            if (roleSuffixIndex == -1)
            {
                return trackingId;
            }

            return trackingId.Substring(0, roleSuffixIndex);
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
