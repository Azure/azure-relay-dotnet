// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal static partial class AuthenticationHelper
    {
        public static string GetBasicAuthChallengeResponse(NetworkCredential credential)
        {
            string authString = !string.IsNullOrEmpty(credential.Domain) ?
                credential.Domain + "\\" + credential.UserName + ":" + credential.Password :
                credential.UserName + ":" + credential.Password;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        }

        public static string GetDigestAuthChallengeResponse(NetworkCredential credential, string httpMethod, string pathAndQuery, string content, string challengeData)
        {
            var digestResponse = new DigestResponse(challengeData);
            return GetDigestTokenForCredential(credential, httpMethod, pathAndQuery, content, digestResponse);
        }
    }
}
