//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;

    /// <summary>
    /// EventSource for the new Dynamic EventSource type of Microsoft-Azure-Relay traces.
    /// 
    /// The default Level is Informational
    /// 
    /// When defining Start/Stop tasks, the StopEvent.Id must be exactly StartEvent.Id + 1.
    /// 
    /// Do not explicity include the Guid here, since EventSource has a mechanism to automatically
    /// map to an EventSource Guid based on the Name (Microsoft-Azure-Relay).  The Guid will 
    /// be consistent as long as the name stays Microsoft-Azure-Relay
    /// </summary>
    [EventSource(Name = "Microsoft-Azure-Relay")]
    class RelayEventSource : EventSource
    {
        public static readonly RelayEventSource Log = new RelayEventSource();

        // Prevent additional instances other than RelayEventSource.Log
        RelayEventSource()
        {
        }

        [Event(40191, Message = "Relay object is online: Source: {0}.")]
        public void RelayClientGoingOnline(string source)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40191, source);
            }
        }

        [Event(40193, Message = "Relay object stop connecting: Source: {0}, ListenerType: {1}.")]
        public void RelayClientStopConnecting(string source, string listenerType)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40193, source, listenerType);
            }
        }

        [Event(40199, Message = "Relay object is reconnecting: Source: {0}.")]
        public void RelayClientReconnecting(string source)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40199, source);
            }
        }

        [Event(40200, Message = "Relay object is disconnected: Source: {0}, IsListener: {1}, Error: {2}.")]
        public void RelayClientDisconnected(string source, bool isListener, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40200, source, isListener, exception);
            }
        }

        [NonEvent]
        public void RelayClientConnectStart(object source)
        {
            if (this.IsEnabled())
            {
                this.RelayClientConnectStart(CreateSourceString(source));
            }
        }

        [Event(40201, Message = "Relay object Connect start: Source: {0}.")]
        void RelayClientConnectStart(string source)
        {
            this.WriteEvent(40201, source);
        }

        [NonEvent]
        public void RelayClientConnectStop(object source)
        {
            if (this.IsEnabled())
            {
                this.RelayClientConnectStop(CreateSourceString(source));
            }
        }

        [Event(40202, Message = "Relay object Connect stop: Source: {0}.")]
        void RelayClientConnectStop(string source)
        {
            this.WriteEvent(40202, source);
        }

        // 40203 Available

        [Event(40204, Message = "Relay Listener received a connection request. Source: {0}, ConnectionId: {1}, Rendezvous Address: {2}.")]
        public void RelayListenerRendezvousStart(string source, string clientId, string rendezvousAddress)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40204, source, clientId, rendezvousAddress);
            }
        }

        [Event(40205, Message = "Relay Listener accepted a client connection. Source: {0}, ConnectionId: {1}.")]
        public void RelayListenerRendezvousStop(string source, string clientId)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40205, source, clientId);
            }
        }

        [NonEvent]
        public void RelayListenerRendezvousFailed(object source, string clientId, string error)
        {
            if (this.IsEnabled())
            {
                this.RelayListenerRendezvousFailed(CreateSourceString(source), clientId, error);
            }
        }

        [Event(40206, Level = EventLevel.Warning, Message = "Relayed Listener failed to accept client. Source: {0}, ConnectionId: {1}, Exception: {2}.")]
        void RelayListenerRendezvousFailed(string source, string clientId, string exception)
        {
            this.WriteEvent(40206, source, clientId, exception);
        }

        [Event(40207, Level = EventLevel.Warning, Message = "Relayed Listener received an unknown command. Source: {0}, Command: {1}.")]
        public void RelayListenerUnknownCommand(string source, string command)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40207, source, command);
            }
        }

        [Event(40209, Level = EventLevel.Warning, Message = "Relay Client failed to acquire token. Source: {0}, IsListener: {1}, Action: {2}, Exception: {3}.")]
        public void RelayClientFailedToAcquireToken(string source, bool isListener, string action, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40209, source, isListener, action, exception);
            }
        }

        [Event(40210, Level = EventLevel.Warning, Message = "RelayedOnewayTcpListener failed to dispatch message. Listener Uri: {0} Message Uri: {1}.")]
        public void RelayListenerFailedToDispatchMessage(string endpoint, string incomingVia)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40210, endpoint, incomingVia);
            }
        }

        [NonEvent]
        public void RelayClientCloseStart(object source)
        {
            if (this.IsEnabled())
            {
                this.RelayClientCloseStart(CreateSourceString(source));
            }
        }

        [Event(40212, Message = "Relay object closing. Source: {0}.")]
        void RelayClientCloseStart(string source)
        {
            this.WriteEvent(40212, source);
        }

        [Event(40213, Message = "Relay object closed.")]
        public void RelayClientCloseStop()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40213);
            }
        }

        [NonEvent]
        public void RelayClientCloseException(object source, Exception exception)
        {
            if (this.IsEnabled())
            {
                this.RelayClientCloseException(CreateSourceString(source), ExceptionToString(exception));
            }
        }

        [Event(40214, Message = "Relay object closing encountered exception: Source: {0}, Exception: {1}.", Level = EventLevel.Warning)]
        void RelayClientCloseException(string source, string exception)
        {
            this.WriteEvent(40214, source, exception);
        }

        [NonEvent]
        public void RelayClientShutdownStart(object source)
        {
            if (this.IsEnabled())
            {
                this.RelayClientShutdownStart(CreateSourceString(source));
            }
        }

        [Event(40215, Message = "Relay object Shutdown beginning. Source: {0}")]
        void RelayClientShutdownStart(string source)
        {
            this.WriteEvent(40215, source);
        }

        [Event(40216, Message = "Relay object Shutdown complete.")]
        public void RelayClientShutdownStop()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40216);
            }
        }

        [Event(40217, Message = "Exception during FramingOuputPump.Ping. Uri {0} Exception {1}.")]
        public void FramingOuputPumpPingException(string endpoint, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40217, endpoint, exception);
            }
        }

        [Event(40218, Message = "Exception during FramingOuputPump.Run. Uri {0} Exception {1}.")]
        public void FramingOuputPumpRunException(string endpoint, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40218, endpoint, exception);
            }
        }

        [Event(40219, Message = "WebStream.Dispose. Uri: {0} Full Uri: {1}.")]
        public void WebStreamDispose(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40219, endpoint, sbUri);
            }
        }

        [Event(40220, Message = "WebStream.Reset. Uri: {0} Full Uri: {1}.")]
        public void WebStreamReset(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40220, endpoint, sbUri);
            }
        }

        [Event(40221, Message = "WebStream.Close. Uri: {0} Full Uri: {1}.")]
        public void WebStreamClose(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40221, endpoint, sbUri);
            }
        }

        [Event(40222, Message = "WebStream.Shutdown. Uri: {0}  Full Uri: {1}.")]
        public void WebStreamShutdown(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40222, endpoint, sbUri);
            }
        }

        [Event(40223, Message = "WebStreamConnection.Abort. Uri: {0}  Full Uri: {1}.")]
        public void WebStreamConnectionAbort(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40223, endpoint, sbUri);
            }
        }

        [Event(40224, Message = "WebStreamConnection.Close. Uri: {0}  Full Uri: {1}.")]
        public void WebStreamConnectionClose(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40224, endpoint, sbUri);
            }
        }

        [Event(40225, Message = "WebStreamConnection.Shutdown. Uri: {0}  Full Uri: {1}.")]
        public void WebStreamConnectionShutdown(string endpoint, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40225, endpoint, sbUri);
            }
        }

        [Event(40226, Level = EventLevel.Error, Message = "Client failed to establish connection. Uri: {0}, Reason: {1}.")]
        public void RelayClientConnectFailed(string endpoint, string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40226, endpoint, message);
            }
        }

        [Event(40227, Message = "RelayClientConnect Redirected from {0} to {1}.")]
        public void RelayClientConnectRedirected(string originalUri, string redirectedUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40227, originalUri, redirectedUri);
            }
        }

        [Event(40228, Message = "WebStream Connecting. Uri: {0} Full Uri: {1} Retries remaining: {2}.")]
        public void WebStreamConnectStart(string originalUri, string sbUri, int retries)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40228, originalUri, sbUri, retries);
            }
        }

        [Event(40229, Message = "WebStream Connected. Uri: {0} Full Uri: {1} Retries remaining: {2}.")]
        public void WebStreamConnectStop(string originalUri, string sbUri, int retries)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40229, originalUri, sbUri, retries);
            }
        }

        [Event(40230, Level = EventLevel.Error, Message = "WebStream Connect Failed. Uri: {0} Full Uri: {1} Retries remaining: {2} Exception {3}.")]
        public void WebStreamConnectFailed(string originalUri, string sbUri, int retries, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40230, originalUri, sbUri, retries, exception);
            }
        }

        [Event(40231, Level = EventLevel.Error, Message = "WebStream FramingInputPump Read Threw Exception with Slow time. Uri: {0} Elapsed: {1} Exception {2}.")]
        public void WebStreamFramingInputPumpSlowReadWithException(string originalUri, string elapsed, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40231, originalUri, elapsed, exception);
            }
        }

        [Event(40232, Level = EventLevel.Warning, Message = "WebStream FramingInputPump Read Was Slow To Read Bytes. Uri: {0} Bytes Received: {1} Elapsed: {2}.")]
        public void WebStreamFramingInputPumpSlowRead(string originalUri, int bytesRead, string elapsed)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40232, originalUri, bytesRead, elapsed);
            }
        }

        [Event(40233, Level = EventLevel.Error, Message = "WebStream FramingOutputPump Ping Write Threw Exception with Slow time. Uri: {0} Elapsed: {1} Exception {2}.")]
        public void WebStreamFramingOuputPumpPingSlowException(string originalUri, string elapsed, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40233, originalUri, elapsed, exception);
            }
        }

        [Event(40234, Message = "WebStream ReadStream OnCompleted fired. Uri: {0} Full Uri: {1}.")]
        public void WebStreamReadStreamCompleted(string originalUri, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40234, originalUri, sbUri);
            }
        }

        [Event(40235, Message = "WebStream WriteStream OnCompleted fired. Uri: {0} Full Uri: {1}.")]
        public void WebStreamWriteStreamCompleted(string originalUri, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40235, originalUri, sbUri);
            }
        }

        [Event(40236, Level = EventLevel.Warning, Message = "WebStream FramingOutputPump Ping Write Was Slow To Read Bytes. Uri: {0} Elapsed: {1}.")]
        public void WebStreamFramingOuputPumpPingSlow(string originalUri, string elapsed)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40236, originalUri, elapsed);
            }
        }

        [Event(40237, Message = "WebStream.Read returning 0. Uri: {0} Full Uri: {1}.")]
        public void WebStreamReturningZero(string originalUri, string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40237, originalUri, sbUri);
            }
        }

        [Event(40238, Level = EventLevel.Error, Message = "WebStream FramingOutputPump WriteThrew Exception with Slow time. Uri: {0} Elapsed: {1} Exception {2}.")]
        public void WebStreamFramingOuputPumpSlowException(string originalUri, string elapsed, string exception)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40238, originalUri, elapsed, exception);
            }
        }

        [Event(40239, Level = EventLevel.Warning, Message = "WebStream FramingOutputPump Write Was Slow To Read Bytes. Uri: {0} Elapsed: {1}.")]
        public void WebStreamFramingOuputPumpSlow(string originalUri, string elapsed)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40239, originalUri, elapsed);
            }
        }

        [Event(40241, Message = "WebSocketConnection.Established Uri: {0}.")]
        public void WebSocketConnectionEstablished(string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40241, sbUri);
            }
        }

        [Event(40242, Message = "WebSocketConnection.Shutdown Uri: {0}.")]
        public void WebSocketConnectionShutdown(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40242, uri);
            }
        }

        [Event(40243, Message = "WebSocketConnection.Close Uri: {0}.")]
        public void WebSocketConnectionClosed(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40243, uri);
            }
        }

        [Event(40244, Level = EventLevel.Warning, Message = "WebSocketConnection.Abort Uri: {0}.")]
        public void WebSocketConnectionAborted(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40244, uri);
            }
        }

        [Event(40245, Message = "WebSocketTransport.Established Uri: {0}.")]
        public void WebSocketTransportEstablished(string sbUri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40245, sbUri);
            }
        }

        [Event(40246, Message = "WebSocketTransport.Shutdown Uri: {0}.")]
        public void WebSocketTransportShutdown(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40246, uri);
            }
        }

        [Event(40247, Message = "WebSocketTransport.Close Uri: {0}.")]
        public void WebSocketTransportClosed(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40247, uri);
            }
        }

        [Event(40248, Level = EventLevel.Warning, Message = "WebSocketTransport.Abort Uri: {0}.")]
        public void WebSocketTransportAborted(string uri)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40248, uri);
            }
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsInformation(object source, Exception exception)
        {
            if (this.IsEnabled())
            {
                this.HandledExceptionAsInformation(CreateSourceString(source), ExceptionToString(exception));
            }
        }

        [Event(40249, Message = "{0} Handled Exception: {1}")]
        void HandledExceptionAsInformation(string source, string exception)
        {
            this.WriteEvent(40249, source, exception);
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsWarning(object source, Exception exception)
        {
            if (this.IsEnabled())
            {
                this.HandledExceptionAsWarning(CreateSourceString(source), ExceptionToString(exception));
            }
        }

        [Event(40250, Level = EventLevel.Warning, Message = "{0} Handled Exception: {1}")]
        void HandledExceptionAsWarning(string source, string exception)
        {
            this.WriteEvent(40250, source, exception);
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsError(object source, Exception exception)
        {
            if (this.IsEnabled())
            {
                this.HandledExceptionAsError(CreateSourceString(source), ExceptionToString(exception));
            }
        }

        [Event(40251, Level = EventLevel.Error, Message = "{0} Handled Exception: {1}")]
        void HandledExceptionAsError(string source, string exception)
        {
            this.WriteEvent(40251, source, exception);
        }

        [NonEvent]
        public void GetTokenStart(object source)
        {
            if (this.IsEnabled())
            {
                this.GetTokenStart(CreateSourceString(source));
            }
        }

        [Event(40255, Level = EventLevel.Informational, Message = "GetToken start. Source: {0}")]
        void GetTokenStart(string source)
        {
            this.WriteEvent(40255, source);
        }

        [NonEvent]
        public void GetTokenStop(DateTime tokenExpiry)
        {
            if (this.IsEnabled())
            {
                this.GetTokenStop(DateTimeToString(tokenExpiry));
            }
        }

        [Event(40256, Level = EventLevel.Informational, Message = "GetToken stop. New token expires at {0}.")]
        void GetTokenStop(string tokenExpiry)
        {
            this.WriteEvent(40256, tokenExpiry);
        }

        [NonEvent]
        public void TokenRenewScheduled(TimeSpan interval, object source)
        {
            if (this.IsEnabled())
            {
                this.TokenRenewScheduled(interval.ToString(), CreateSourceString(source));
            }
        }

        [Event(40257, Level = EventLevel.Informational, Message = "Scheduling Token renewal after {0}. Source: {1}.")]
        void TokenRenewScheduled(string interval, string source)
        {
            this.WriteEvent(40257, interval, source);
        }

        [Event(40260, Level = EventLevel.Informational, Message = "Detecting connectivity mode succeeded: Endpoint: {0}, TriedMode: {1}")]
        public void DetectConnectivityModeSucceeded(string endpoint, string mode)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40260, endpoint, mode);
            }
        }

        [Event(40261, Level = EventLevel.Warning, Message = "Detecting connectivity mode failed: Endpoint: {0}, TriedMode: {1}")]
        public void DetectConnectivityModeFailed(string endpoint, string mode)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40261, endpoint, mode);
            }
        }

        [NonEvent]
        public ArgumentNullException ArgumentNull(string paramName, object source = null, EventLevel level = EventLevel.Error)
        {
            return this.ThrowingException(new ArgumentNullException(paramName), source, level);
        }

        [NonEvent]
        public ArgumentException Argument(string paramName, string message, object source = null, EventLevel level = EventLevel.Error)
        {
            return this.ThrowingException(new ArgumentException(message, paramName), source, level);
        }

        [NonEvent]
        public ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, object actualValue, string message, object source = null, EventLevel level = EventLevel.Error)
        {
            return this.ThrowingException(new ArgumentOutOfRangeException(paramName, actualValue, message), source, level);
        }

        [NonEvent]
        public TException ThrowingException<TException>(TException exception, object source = null, EventLevel level = EventLevel.Error)
            where TException : Exception
        {
            // Avoid converting ToString, etc. if ETW tracing is not enabled.
            if (this.IsEnabled())
            {
                switch (level)
                {
                    case EventLevel.Critical:
                    case EventLevel.LogAlways:
                    case EventLevel.Error:
                        this.ThrowingExceptionError(CreateSourceString(source), ExceptionToString(exception));
                        break;
                    case EventLevel.Warning:
                        this.ThrowingExceptionWarning(CreateSourceString(source), ExceptionToString(exception));
                        break;
                    case EventLevel.Informational:
                    case EventLevel.Verbose:
                    default:
                        this.ThrowingExceptionInfo(CreateSourceString(source), ExceptionToString(exception));
                        break;
                }
            }

            // This allows "throw ServiceBusEventSource.Log.ThrowingException(..."
            return exception;
        }

        [Event(40262, Level = EventLevel.Error, Message = "{0} Throwing an Exception: {1}")]
        void ThrowingExceptionError(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40262, source, exception);
        }

        [Event(40263, Level = EventLevel.Warning, Message = "{0} Throwing an Exception: {1}")]
        void ThrowingExceptionWarning(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40263, source, exception);
        }

        [Event(40264, Level = EventLevel.Informational, Message = "{0} Throwing an Exception: {1}")]
        void ThrowingExceptionInfo(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40264, source, exception);
        }
        
        [NonEvent]
        internal static string CreateSourceString(object source)
        {
            Type type;
            string s;
            if (source == null)
            {
                return string.Empty;
            }
            else if ((type = source as Type) != null)
            {
                return type.Name;
            }
            else if ((s = source as string) != null)
            {
                return s;
            }

            return source.ToString();
        }

        [NonEvent]
        static string ExceptionToString(Exception exception)
        {
            return exception?.ToString() ?? string.Empty;
        }

        [NonEvent]
        static string DateTimeToString(DateTime dateTime)
        {
            return dateTime.ToString(CultureInfo.InvariantCulture);
        }

        public class Keywords   // This is a bitvector
        {
            //public const EventKeywords Client = (EventKeywords)0x0001;
            //public const EventKeywords Relay = (EventKeywords)0x0002;
            //public const EventKeywords Messaging = (EventKeywords)0x0002;
        }
    }
}
