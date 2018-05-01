// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;

    /// <summary>
    /// TrackingContext class is used for correlating end to end tracing of operations.
    /// </summary>
    public sealed class TrackingContext
    {
        static readonly int GuidStringLength = Guid.Empty.ToString().Length;
        internal const string TrackingIdName = "TrackingId";
        const string AddressName = "Address";
        const string TimestampName = "Timestamp";
        string cachedToString;

        TrackingContext(Guid activityId, string trackingId, string address)
        {
            this.ActivityId = activityId;
            this.TrackingId = trackingId;
            this.Address = address;
        }

        /// <summary> Returns the the Guid representation of the tracking id.</summary>
        public Guid ActivityId { get; }

        /// <summary> Returns the tracking id used in this context.</summary>
        public string TrackingId { get; }

        /// <summary> Returns the address associated with the current tracking context. </summary>
        public string Address { get; }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and no address.
        /// </summary>
        internal static TrackingContext Create()
        {
            return Create(Guid.NewGuid(), (string)null);
        }

        /// <summary>
        /// Create a TrackingContext with a new Guid/TrackingId and given address.
        /// </summary>
        /// <param name="address">subsystem-specific Uri like entity address to be used in the tracking context</param>
        internal static TrackingContext Create(Uri address)
        {
            return Create(Guid.NewGuid(), address.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(Guid activityId, string address)
        {
            return Create(activityId, activityId.ToString(), address);
        }

        internal static TrackingContext Create(string trackingId, Uri address)
        {
            return Create(trackingId, address.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(string trackingId, string address)
        {
            Guid activityId;
            bool parseFailed = false;
            if (!Guid.TryParse(trackingId.Substring(0, Math.Min(GuidStringLength, trackingId.Length)), out activityId))
            {
                parseFailed = true;
                activityId = Guid.NewGuid();
            }

            var trackingContext = Create(activityId, trackingId, address);
            if (parseFailed)
            {
                RelayEventSource.Log.Info(nameof(TrackingContext), trackingContext, $"Parsing TrackingId:'{trackingId}' as Guid failed, created new ActivityId:{activityId} for trace correlation.");
            }

            return trackingContext;
        }

        internal static TrackingContext Create(Guid activityId, Uri address)
        {
            return Create(activityId, activityId.ToString(), address);
        }

        internal static TrackingContext Create(Guid activityId, string trackingId, Uri address)
        {
            return Create(activityId, trackingId, address.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        }

        internal static TrackingContext Create(Guid activityId, string trackingId, string address)
        {
            return new TrackingContext(activityId, trackingId, address);
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

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            if (this.cachedToString == null)
            {
                if (string.IsNullOrEmpty(this.Address))
                {
                    this.cachedToString = TrackingIdName + ":" + this.TrackingId;
                }
                else
                {
                    this.cachedToString = TrackingIdName + ":" + this.TrackingId + ", " + AddressName + ":" + this.Address;
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
            return string.IsNullOrWhiteSpace(this.Address) ?
                TrackingIdName + ":" + this.TrackingId + ", " + TimestampName + ":" + timestamp :
                TrackingIdName + ":" + this.TrackingId + ", " + AddressName + ":" + this.Address + ", " + TimestampName + ":" + timestamp;
        }
    }
}
