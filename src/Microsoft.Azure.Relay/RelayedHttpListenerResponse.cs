// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a response to a request being handled by a <see cref="HybridConnectionListener"/> object.
    /// This is modeled after System.Net.HttpListenerResponse.
    /// </summary>
    public sealed class RelayedHttpListenerResponse : ITraceSource, IDisposable
    {
        bool readOnly;
        bool disposed;
        HttpStatusCode statusCode;
        string statusDescription;

        internal RelayedHttpListenerResponse(RelayedHttpListenerContext context)
        {
            this.Context = context;
            this.statusCode = HttpStatusCode.Continue;
            this.Headers = new ResponseWebHeaderCollection(this);
            this.OutputStream = Stream.Null;
        }

        /// <summary>
        /// Gets the collection of header name/value pairs to be returned by this listener.
        /// </summary>
        public WebHeaderCollection Headers { get; }

        /// <summary>Gets a <see cref="T:System.IO.Stream" /> object to which a response can be written.</summary>
        public Stream OutputStream { get; internal set; }

        /// <summary>Gets or sets the HTTP status code to be returned to the client.</summary>
        /// <exception cref="ObjectDisposedException">This object is closed.</exception>
        /// <exception cref="ProtocolViolationException">The value specified for a set operation is not valid. Valid values are between 100 and 999 inclusive.</exception>
        /// <exception cref="InvalidOperationException">An attempt was made to change this value after writing to the output stream.</exception>
        public HttpStatusCode StatusCode
        {
            get
            {
                return this.statusCode;
            }
            set
            {
                this.CheckDisposedOrReadOnly();
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
        /// <exception cref="InvalidOperationException">An attempt was made to change this value after writing to the output stream.</exception>
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
                this.CheckDisposedOrReadOnly();
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

        TrackingContext ITraceSource.TrackingContext => this.Context.TrackingContext;

        RelayedHttpListenerContext Context { get; }

        /// <summary>Sends the response to the client and releases the resources held by this <see cref="RelayedHttpListenerResponse"/> instance.</summary>
        public void Close()
        {
            this.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>Sends the response to the client and releases the resources held by this <see cref="RelayedHttpListenerResponse"/> instance.</summary>
        public async Task CloseAsync()
        {
            try
            {
                var closeAsync = this.OutputStream as ICloseAsync;
                if (closeAsync != null)
                {
                    await closeAsync.CloseAsync().ConfigureAwait(false);
                }
                else
                {
                    this.OutputStream.Dispose();
                }
            }
            catch (Exception e) when (!Fx.IsFatal(e))
            {
                RelayEventSource.Log.ThrowingException(e, this);
                throw;
            }
            finally
            {
                ((IDisposable)this).Dispose();
            }
        }

        void IDisposable.Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.OutputStream.Dispose();
                this.disposed = true;
            }
        }

        internal void SetReadOnly()
        {
            this.readOnly = true;
        }

        void CheckDisposedOrReadOnly()
        {
            if (this.disposed)
            {
                string message = this.Context.TrackingContext.EnsureTrackableMessage(this.ToString());
                throw RelayEventSource.Log.ThrowingException(new ObjectDisposedException(message), this);
            }

            if (this.readOnly)
            {
                string message = this.Context.TrackingContext.EnsureTrackableMessage(SR.ResponseBodyStarted);
                throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(message), this);
            }
        }

        class ResponseWebHeaderCollection : WebHeaderCollection
        {
            readonly RelayedHttpListenerResponse response;

            public ResponseWebHeaderCollection(RelayedHttpListenerResponse response)
            {
                this.response = response;
            }

            public override void Add(string name, string value)
            {
                this.response.CheckDisposedOrReadOnly();
                base.Add(name, value);
            }
            
            public override void Clear()
            {
                this.response.CheckDisposedOrReadOnly();
                base.Clear();
            }

            public override void Remove(string name)
            {
                this.response.CheckDisposedOrReadOnly();
                base.Remove(name);
            }

            public override void Set(string name, string value)
            {
                this.response.CheckDisposedOrReadOnly();
                base.Set(name, value);
            }
        }
    }
}
