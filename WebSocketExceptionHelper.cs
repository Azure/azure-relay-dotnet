//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
            Exception innerException = exception;

            if (exception is WebSocketException)
            {
                WebException innerWebException;
                IOException innerIOException;
                if ((innerWebException = exception.InnerException as WebException) != null)
                {
                    innerException = innerWebException;
                    HttpWebResponse httpWebResponse;
                    if ((httpWebResponse = innerWebException.Response as HttpWebResponse) != null)
                    {
                        message = httpWebResponse.StatusDescription;
                        switch (httpWebResponse.StatusCode)
                        {
                            case HttpStatusCode.BadRequest:
                                return new RelayException(httpWebResponse.StatusCode + ": " + httpWebResponse.StatusDescription, innerWebException);
                            case HttpStatusCode.Unauthorized:
                                return new AuthorizationFailedException(httpWebResponse.StatusDescription, innerWebException);
                            case HttpStatusCode.NotFound:
                                return new EndpointNotFoundException(httpWebResponse.StatusDescription, innerWebException);
                            case HttpStatusCode.GatewayTimeout:
                            case HttpStatusCode.RequestTimeout:
                                // TODO: Add a way to tell if the listener failed to rendezvous or if the timeout was the application.
                                return new TimeoutException(httpWebResponse.StatusDescription, innerWebException);
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
                }
                else if ((innerIOException = exception.InnerException as IOException) != null)
                {
                    innerException = innerIOException;
                    message = innerIOException.Message;
                }

            }

            return new RelayException(message, innerException);
        }
    }
}
