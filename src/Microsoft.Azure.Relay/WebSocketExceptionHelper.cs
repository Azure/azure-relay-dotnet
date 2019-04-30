// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;

    static class WebSocketExceptionHelper
    {
        public static bool IsRelayContract(Exception exception)
        {
            return Fx.IsFatal(exception) ||
                exception is RelayException || 
                exception is TimeoutException;
        }

        public static Exception ConvertToRelayContract(Exception exception, TrackingContext trackingContext, HttpResponseMessage httpResponseMessage = null, bool isListener = true)
        {
            string message = exception.Message;
            if (IsRelayContract(exception))
            {
                return exception;
            }
            else if (exception is WebSocketException)
            {
                WebException innerWebException;
                IOException innerIOException;
                SocketException socketException;
                if ((innerWebException = exception.InnerException as WebException) != null)
                {
                    HttpWebResponse httpWebResponse;
                    if ((httpWebResponse = innerWebException.Response as HttpWebResponse) != null)
                    {
                        return CreateExceptionForStatus(httpWebResponse.StatusCode, httpWebResponse.StatusDescription, exception, trackingContext, isListener);
                    }
                    else if (innerWebException.Status == WebExceptionStatus.NameResolutionFailure)
                    {
                        message = innerWebException.Message;
                    }
                }
                else if ((innerIOException = exception.InnerException as IOException) != null)
                {
                    message = innerIOException.Message;
                }
                else if ((socketException = exception.InnerException as SocketException) != null)
                {
                    if (socketException.SocketErrorCode == SocketError.HostNotFound)
                    {
                        message = socketException.Message;
                    }
                }
                else if (httpResponseMessage != null)
                {
                    return CreateExceptionForStatus(httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase, exception, trackingContext, isListener);
                }
            }

            if (trackingContext != null)
            {
                message = trackingContext.EnsureTrackableMessage(message);
            }

            return new RelayException(message, exception);
        }

        static Exception CreateExceptionForStatus(HttpStatusCode statusCode, string statusDescription, Exception inner, TrackingContext trackingContext, bool isListener)
        {
            if (trackingContext != null)
            {
                statusDescription = trackingContext.EnsureTrackableMessage(statusDescription);
            }

            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return new AuthorizationFailedException(statusDescription, inner);
                case HttpStatusCode.NotFound:
                    return new EndpointNotFoundException(statusDescription, inner, isTransient: !isListener);
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.RequestTimeout:
                    return new TimeoutException(statusCode + ": " + statusDescription, inner);
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.NotImplemented:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.ServiceUnavailable:
                default:
                    return new RelayException(statusCode + ": " + statusDescription, inner);
            }
        }
    }
}
