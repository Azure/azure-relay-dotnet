// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;

    /// <summary>
    /// Represents a response to a request being handled by a <see cref="HybridConnectionListener"/> object.
    /// This is modeled after System.Net.HttpListenerResponse.
    /// </summary>
    public sealed class RelayedHttpListenerResponse
    {
        HttpStatusCode statusCode;
        string statusDescription;

        internal RelayedHttpListenerResponse(RelayedHttpListenerContext context)
        {
            this.Context = context;
            this.statusCode = HttpStatusCode.Continue;
            this.Headers = new WebHeaderCollection();
        }

        /// <summary>
        /// Gets the collection of header name/value pairs to be returned by this listener.
        /// </summary>
        public WebHeaderCollection Headers { get; }

        RelayedHttpListenerContext Context { get; }

        /// <summary>Gets or sets the HTTP status code to be returned to the client.</summary>
        /// <exception cref="ObjectDisposedException">This object is closed.</exception>
        /// <exception cref="ProtocolViolationException">The value specified for a set operation is not valid. Valid values are between 100 and 999 inclusive.</exception>
        public HttpStatusCode StatusCode
        {
            get
            {
                return this.statusCode;
            }
            set
            {
                int valueInt = (int)value;
                if (valueInt < 100 || valueInt > 999)
                {
                    throw RelayEventSource.Log.ThrowingException(new ProtocolViolationException(SR.net_InvalidStatus), this.Context);
                }

                this.statusCode = value;
            }
        }

        /// <summary>Gets or sets a text description of the HTTP status code returned to the client.</summary>
        /// <returns>The text description of the HTTP status code returned to the client.</returns>
        /// <exception cref="ArgumentNullException">The value specified for a set operation is null.</exception>
        /// <exception cref="ArgumentException">The value specified for a set operation contains non-printable characters.</exception>
        public string StatusDescription
        {
            get
            {
                if (this.statusDescription == null)
                {
                    // if the user hasn't set this, generate on the fly, if possible.
                    // We know this one is safe, no need to verify it as in the setter.
                    this.statusDescription = HttpStatusDescription.Get(this.StatusCode);
                }

                if (this.statusDescription == null)
                {
                    this.statusDescription = string.Empty;
                }

                return this.statusDescription;
            }
            set
            {
                if (value == null)
                {
                    throw RelayEventSource.Log.ThrowingException(new ArgumentNullException(nameof(value)), this.Context);
                }

                // Need to verify the status description doesn't contain any control characters except HT.
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if ((c <= 31 && c != '\t') || c >= 127)
                    {
                        throw RelayEventSource.Log.ThrowingException(new ArgumentException(SR.net_WebHeaderInvalidControlChars, nameof(value)), this.Context);
                    }
                }

                this.statusDescription = value;
            }
        }
    }
}
