﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets.NetCore21
{
    using System;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// This is a reflection based wrapper around System.Net.Security.SslClientAuthenticationOptions
    /// which is publicly supported from netcoreapp2.1 but isn't part of netstandard2.0.
    /// </summary>
    class SslClientAuthenticationOptions : ObjectAccessor
    {
        static bool? isSupported;

        public SslClientAuthenticationOptions(object runtimeInstance)
            : base(runtimeInstance)
        {
        }

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback
        {
            get => this.GetProperty<RemoteCertificateValidationCallback>(nameof(RemoteCertificateValidationCallback));
            set => this.SetProperty(nameof(RemoteCertificateValidationCallback), value);
        }

        public X509CertificateCollection ClientCertificates
        {
            get => this.GetProperty<X509CertificateCollection>(nameof(ClientCertificates));
            set => this.SetProperty(nameof(ClientCertificates), value);
        }

        internal static bool IsSupported()
        {
            if (!isSupported.HasValue)
            {
                Type sslCientOptionsType = typeof(SslStream).Assembly.GetType(
                    "System.Net.Security.SslClientAuthenticationOptions", throwOnError: false);
                isSupported = sslCientOptionsType != null;
            }

            return isSupported.Value;
        }
    }
}