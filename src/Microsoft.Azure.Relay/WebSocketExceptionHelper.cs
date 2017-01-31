// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;

    static class WebSocketExceptionHelper
    {
        public static Exception ConvertToRelayContract(Exception exception)
        {
            string message = exception.Message;
            if (exception is WebSocketException)
            {
                WebException innerWebException;
                IOException innerIOException;
                if ((innerWebException = exception.InnerException as WebException) != null)
                {
                    HttpWebResponse httpWebResponse;
                    if ((httpWebResponse = innerWebException.Response as HttpWebResponse) != null)
                    {
                        message = httpWebResponse.StatusDescription;
                        switch (httpWebResponse.StatusCode)
                        {
                            case HttpStatusCode.BadRequest:
                                return new RelayException(httpWebResponse.StatusCode + ": " + httpWebResponse.StatusDescription, exception);
                            case HttpStatusCode.Unauthorized:
                                return new AuthorizationFailedException(httpWebResponse.StatusDescription, exception);
                            case HttpStatusCode.NotFound:
                                return new EndpointNotFoundException(httpWebResponse.StatusDescription, exception);
                            case HttpStatusCode.GatewayTimeout:
                            case HttpStatusCode.RequestTimeout:
                                // TODO: Add a way to tell if the listener failed to rendezvous or if the timeout was the application.
                                return new TimeoutException(httpWebResponse.StatusDescription, exception);
                            // Other values we might care about
                            case HttpStatusCode.InternalServerError:
                            case HttpStatusCode.NotImplemented:
                            case HttpStatusCode.BadGateway:
                            case HttpStatusCode.ServiceUnavailable:
                                break;
                            default:
                                break;
                        }
                    }
                    else if (innerWebException.Status == WebExceptionStatus.NameResolutionFailure)
                    {
                        return new EndpointNotFoundException(innerWebException.Message, exception);
                    }
                }
                else if ((innerIOException = exception.InnerException as IOException) != null)
                {
                    message = innerIOException.Message;
                }
            }

            return new RelayException(message, exception);
        }
    }
}
