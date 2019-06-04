// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    
    /// <summary>
    /// This is a reflection based wrapper around System.Net.Http.SocketsHttpHandler
    /// which is publicly supported from netcoreapp2.1 but isn't part of netstandard2.0.
    /// </summary>
    sealed class SocketsHttpHandler : ObjectAccessor, IDisposable
    {
        static Type socketsHttpHandlerType;
        readonly HttpMessageHandler socketsHttpMessageHandler;
        readonly HttpMessageInvoker invoker;
        SslClientAuthenticationOptions sslOptions;

        public SocketsHttpHandler()
            : base(CreateSocketsHttpHandler())
        {
            this.socketsHttpMessageHandler = (HttpMessageHandler)this.Instance;
            this.invoker = new HttpMessageInvoker(this.socketsHttpMessageHandler, disposeHandler: true);
        }

        public TimeSpan PooledConnectionLifetime
        {
            get => this.GetProperty<TimeSpan>(nameof(PooledConnectionLifetime));
            set => this.SetProperty(nameof(PooledConnectionLifetime), value);
        }

        public CookieContainer CookieContainer
        {
            get => this.GetProperty<CookieContainer>(nameof(CookieContainer));
            set => this.SetProperty(nameof(CookieContainer), value);
        }

        public bool UseCookies
        {
            get => this.GetProperty<bool>(nameof(UseCookies));
            set => this.SetProperty(nameof(UseCookies), value);
        }

        public SslClientAuthenticationOptions SslOptions
        {
            get
            {
                if (this.sslOptions == null)
                {
                    object innerSslOptions = this.GetProperty<object>(nameof(SslOptions));
                    this.sslOptions = new SslClientAuthenticationOptions(innerSslOptions);
                }

                return this.sslOptions;
            }
        }

        public ICredentials Credentials
        {
            get => this.GetProperty<ICredentials>(nameof(Credentials));
            set => this.SetProperty(nameof(Credentials), value);
        }

        public bool UseProxy
        {
            get => this.GetProperty<bool>(nameof(UseProxy));
            set => this.SetProperty(nameof(UseProxy), value);
        }

        public IWebProxy Proxy
        {
            get => this.GetProperty<IWebProxy>(nameof(Proxy));
            set => this.SetProperty(nameof(Proxy), value);
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return this.invoker.SendAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            this.invoker.Dispose();
        }

        static HttpMessageHandler CreateSocketsHttpHandler()
        {
            if (socketsHttpHandlerType == null)
            {
                var systemNetHttpAssembly = typeof(HttpClient).Assembly;
                socketsHttpHandlerType = systemNetHttpAssembly.GetType(
                    "System.Net.Http.SocketsHttpHandler", throwOnError: true);
            }

            return (HttpMessageHandler)Activator.CreateInstance(socketsHttpHandlerType);
        }
    }
}