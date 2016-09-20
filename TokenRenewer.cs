//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Threading.Tasks;
    using System.Globalization;
    using System.Threading;

    class TokenEventArgs : EventArgs
    {
        public SecurityToken Token { get; internal set; }

        public Exception Exception { get; internal set; }
    }

    class TokenRenewer
    {
        static readonly TimeSpan GetTokenTimeout = TimeSpan.FromMinutes(1);
        readonly Timer renewTimer;
        readonly TokenProvider tokenProvider;
        readonly string action;
        readonly string appliesTo;
        readonly object traceSource;

        public TokenRenewer(TokenProvider tokenProvider, string appliesTo, string action, object traceSource)
        {
            Fx.Assert(tokenProvider != null, "tokenProvider is required");
            Fx.Assert(!string.IsNullOrEmpty(appliesTo), "appliesTo is required");
            this.tokenProvider = tokenProvider;
            this.appliesTo = appliesTo;
            this.action = action;
            this.traceSource = traceSource;
            this.renewTimer = new Timer(s => OnRenewTimer(s), this, Timeout.Infinite, Timeout.Infinite);
        }

        public event EventHandler<TokenEventArgs> TokenRenewed;

        public event EventHandler<TokenEventArgs> TokenRenewException;

        object ThisLock
        {
            get { return this.renewTimer; }
        }

        public async Task<SecurityToken> GetTokenAsync(TimeSpan timeout)
        {
            try
            {
                RelayEventSource.Log.GetTokenStart(this.traceSource);
                var token = await this.tokenProvider.GetTokenAsync(this.appliesTo, this.action, timeout);
                RelayEventSource.Log.GetTokenStop(token.ExpiresAtUtc);
                this.OnTokenRenewed(token);
                return token;
            }
            catch (Exception e)
            {
                if (!Fx.IsFatal(e))
                {
                    this.OnTokenRenewException(e);
                }

                throw;
            }
        }

        public void Close()
        {
            this.renewTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        static async void OnRenewTimer(object state)
        {
            var thisPtr = (TokenRenewer)state;
            try
            {
                await thisPtr.GetTokenAsync(GetTokenTimeout);
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                RelayEventSource.Log.HandledExceptionAsWarning(thisPtr.traceSource, exception);
            }
        }

        void ScheduleRenewTimer(SecurityToken token)
        {
            TimeSpan interval = token.ExpiresAtUtc.Subtract(DateTime.UtcNow);
            if (interval < TimeSpan.Zero)
            {
                // TODO: RelayEventSource.Log.WcfEventWarning(Diagnostics.TraceCode.Security, this.traceSource, "Not renewing since " + interval + " < TimeSpan.Zero!");
                return;
            }

            // TokenProvider won't return a token which is within 5min of expiring so we don't have to pad here.
            interval = interval < RelayConstants.ClientMinimumTokenRefreshInterval ? RelayConstants.ClientMinimumTokenRefreshInterval : interval;

            RelayEventSource.Log.TokenRenewScheduled(interval, this.traceSource);
            this.renewTimer.Change(interval, Timeout.InfiniteTimeSpan);
        }

        void OnTokenRenewed(SecurityToken token)
        {
            this.TokenRenewed?.Invoke(this, new TokenEventArgs { Token = token });
            this.ScheduleRenewTimer(token);
        }

        void OnTokenRenewException(Exception exception)
        {
            this.TokenRenewException?.Invoke(this, new TokenEventArgs { Exception = exception });
        }
    }
}
