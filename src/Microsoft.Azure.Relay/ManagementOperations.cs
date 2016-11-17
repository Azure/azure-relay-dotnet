// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    sealed class ManagementOperations
    {
        static readonly TimeSpan TokenDuration = TimeSpan.FromMinutes(10);

        public static async Task<TEntityDescription> GetAsync<TEntityDescription>(
            Uri resourceUri, TokenProvider tokenProvider, CancellationToken cancellationToken)
        {
            Fx.Assert(resourceUri != null, "resourceUri is required");
            Fx.Assert(tokenProvider != null, "tokenProvider is required");

            var httpClient = new HttpClient();
            try
            {
                httpClient.BaseAddress = CreateManagementUri(resourceUri);
                httpClient.DefaultRequestHeaders.Add("X-PROCESS-AT", "ServiceBus");
                var token = await tokenProvider.GetTokenAsync(resourceUri.AbsoluteUri, TokenDuration).ConfigureAwait(false);
                httpClient.DefaultRequestHeaders.Add("Authorization", token.TokenString);

                var httpResponse = await httpClient.GetAsync(string.Empty, cancellationToken).ConfigureAwait(false);
                if (httpResponse.IsSuccessStatusCode)
                {
                    if (IsFeedContentType(httpResponse))
                    {
                        // REST management operations will return an atom feed for unknown paths
                        throw new EndpointNotFoundException(resourceUri.AbsolutePath.TrimStart('/'));
                    }

                    using (var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        return DeserializeFromAtomEntry<TEntityDescription>(stream);
                    }
                }
                else
                {
                    throw await CreateExceptionForFailedResponseAsync(httpResponse).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (!Fx.IsFatal(exception) && !(exception is RelayException))
            {
                throw WebSocketExceptionHelper.ConvertToRelayContract(exception);
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        static Uri CreateManagementUri(Uri uri)
        {
            // Create an HTTPS Uri for the REST operation
            var builder = new UriBuilder(uri) { Scheme = UriScheme.Https };
            builder.Query = AddQueryParameter(builder.Query, "api-version", RelayConstants.ManagementApiVersion);
            return builder.Uri;
        }

        static string AddQueryParameter(string queryString, string name, string value)
        {
            if (!string.IsNullOrEmpty(queryString))
            {
                queryString += "&";
            }

            return queryString + name + "=" + value;
        }

        static TEntityDescription DeserializeFromAtomEntry<TEntityDescription>(Stream stream)
        {
            // ServiceBus REST GET returns an Atom Entry like this:
            // <entry xmlns="http://www.w3.org/2005/Atom">
            //   ... Atom elements such as <id>, <title>, <published>, <updated>, <author>, <link> ...
            //   <content type="application/xml">
            //     <HybridConnectionDescription...
            Fx.Assert(stream != null, "stream is required");
            using (XmlReader xmlReader = XmlReader.Create(stream))
            {
                // Move to the Atom <content> element then advance to the first non-whitespace element inside it
                xmlReader.ReadToDescendant("content", "http://www.w3.org/2005/Atom");
                xmlReader.Read();
                xmlReader.MoveToContent();

                var serializer = new DataContractSerializer(typeof(TEntityDescription));
                return (TEntityDescription)serializer.ReadObject(xmlReader);
            }
        }

        static bool IsFeedContentType(HttpResponseMessage httpResponse)
        {
            // Looks like application/atom+xml;type=feed;charset=utf-8
            MediaTypeHeaderValue contentType = httpResponse.Content.Headers.ContentType;
            if (contentType != null && string.Equals(contentType.MediaType, "application/atom+xml", StringComparison.OrdinalIgnoreCase))
            {
                ICollection<NameValueHeaderValue> parameters = contentType.Parameters;
                if (parameters != null && parameters.Count > 0)
                {
                    NameValueHeaderValue typeParameter = parameters.FirstOrDefault(nvhv => nvhv.Name.Equals("type", StringComparison.OrdinalIgnoreCase));
                    if (typeParameter != null)
                    {
                        return typeParameter.Value.Equals("feed", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return false;
        }

        static async Task<Exception> CreateExceptionForFailedResponseAsync(HttpResponseMessage httpResponse)
        {
            string detail = await ExtractErrorDetailFromResponseAsync(httpResponse).ConfigureAwait(false);
            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new AuthorizationFailedException(detail);
            }
            else if (httpResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                return new QuotaExceededException(detail);
            }
            else if (httpResponse.StatusCode == HttpStatusCode.NotFound || httpResponse.StatusCode == HttpStatusCode.NoContent)
            {
                return new EndpointNotFoundException(detail);
            }
            else if (httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new ServerBusyException(detail);
            }
            else if (httpResponse.StatusCode == HttpStatusCode.InternalServerError)
            {
                return new RelayException(detail);
            }
            else if (httpResponse.StatusCode == HttpStatusCode.Conflict)
            {
                return new EndpointAlreadyExistsException(detail);
            }

            return new RelayException(detail);
        }

        static async Task<string> ExtractErrorDetailFromResponseAsync(HttpResponseMessage httpResponse)
        {
            try
            {
                // ServiceBus returns error details in the body like this:
                // <Error><Code>XXX</Code><Detail>Message Here. TrackingId:...</Detail></Error>
                using (var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (XmlReader xmlReader = XmlReader.Create(stream))
                {
                    if (xmlReader.ReadToDescendant("Detail"))
                    {
                        string value = xmlReader.ReadInnerXml();
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception exception) when (!Fx.IsFatal(exception))
            {
                RelayEventSource.Log.HandledExceptionAsInformation(nameof(ManagementOperations), exception);
            }

            return httpResponse.ReasonPhrase;
        }
    }
}