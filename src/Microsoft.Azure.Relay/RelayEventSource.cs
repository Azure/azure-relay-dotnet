// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        [NonEvent]
        public void RelayClientConnectStart(object source)
        {
            if (this.IsEnabled())
            {
                this.RelayClientConnectStart(CreateSourceString(source));
            }
        }

        [Event(40201, Message = "Relay object Connect start: {0}.")]
        void RelayClientConnectStart(string source)
        {
            this.WriteEvent(40201, source);
        }

        [Event(40202, Message = "Relay object Connect stop.")]
        public void RelayClientConnectStop()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40202);
            }
        }

        // 40203 Available

        [NonEvent]
        public void RelayListenerRendezvousStart(RelayedHttpListenerContext listenerContext, string rendezvousAddress)
        {
            if (this.IsEnabled())
            {
                this.RelayListenerRendezvousStart(
                    CreateSourceString(listenerContext.Listener), listenerContext.TrackingContext.TrackingId, rendezvousAddress);
            }
        }

        [Event(40204, Message = "Relay Listener received a connection request. {0}, ConnectionId: {1}, Rendezvous Address: {2}.")]
        void RelayListenerRendezvousStart(string source, string clientId, string rendezvousAddress)
        {
            this.WriteEvent(40204, source, clientId, rendezvousAddress);
        }

        [Event(40205, Message = "Relay Listener accepted a client connection.")]
        public void RelayListenerRendezvousStop()
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40205);
            }
        }

        [NonEvent]
        public void RelayListenerRendezvousFailed(RelayedHttpListenerContext listenerContext, string exception)
        {
            if (this.IsEnabled())
            {
                this.RelayListenerRendezvousFailed(
                    CreateSourceString(listenerContext.Listener), listenerContext.TrackingContext.TrackingId, exception);
            }
        }

        [Event(40206, Level = EventLevel.Warning, Message = "Relayed Listener failed to accept client. {0}, ConnectionId: {1}, Exception: {2}.")]
        void RelayListenerRendezvousFailed(string source, string clientId, string exception)
        {
            this.WriteEvent(40206, source, clientId, exception);
        }

        [NonEvent]
        public void RelayListenerRendezvousRejected(RelayedHttpListenerContext listenerContext)
        {
            if (this.IsEnabled())
            {
                var response = listenerContext.Response;
                this.RelayListenerRendezvousRejected(
                    listenerContext.TrackingContext.TrackingId, (int)response.StatusCode, response.StatusDescription);
            }
        }

        [Event(40207, Level = EventLevel.Warning, Message = "Relayed Listener is rejecting the client. ConnectionId: {0}, StatusCode: {1}, StatusDescription: {2}.")]
        void RelayListenerRendezvousRejected(string clientId, int statusCode, string statusDescription)
        {
            this.WriteEvent(40207, clientId, statusCode, statusDescription);
        }

        [Event(40208, Level = EventLevel.Warning, Message = "Relayed Listener received an unknown command. {0}, Command: {1}.")]
        public void RelayListenerUnknownCommand(string source, string command)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(40208, source, command);
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

        [Event(40212, Message = "Relay object closing: {0}.")]
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

        [Event(40214, Message = "Relay object closing encountered exception: {0}, Exception: {1}.", Level = EventLevel.Warning)]
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

        [Event(40215, Message = "Relay object Shutdown beginning: {0}")]
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

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsInformation(object source, Exception exception)
        {
            if (this.IsEnabled())
            {
                this.HandledExceptionAsInformation(CreateSourceString(source), ExceptionToString(exception));
            }
        }

        [Event(40249, Message = "Exception Handled: {0} {1}")]
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

        [Event(40250, Level = EventLevel.Warning, Message = "Exception Handled: {0} {1}")]
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

        [Event(40251, Level = EventLevel.Error, Message = "Exception Handled: {0} {1}")]
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

        [Event(40255, Level = EventLevel.Informational, Message = "GetToken start: {0}")]
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

        [Event(40257, Level = EventLevel.Informational, Message = "Scheduling Token renewal after {0}. {1}.")]
        void TokenRenewScheduled(string interval, string source)
        {
            this.WriteEvent(40257, interval, source);
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

        [Event(40262, Level = EventLevel.Error, Message = "Throwing an Exception: {0} {1}")]
        void ThrowingExceptionError(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40262, source, exception);
        }

        [Event(40263, Level = EventLevel.Warning, Message = "Throwing an Exception: {0} {1}")]
        void ThrowingExceptionWarning(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40263, source, exception);
        }

        [Event(40264, Level = EventLevel.Informational, Message = "Throwing an Exception: {0} {1}")]
        void ThrowingExceptionInfo(string source, string exception)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40264, source, exception);
        }
        
        [NonEvent]
        internal static string CreateSourceString(object source)
        {
            Type type;
            if (source == null)
            {
                return string.Empty;
            }
            else if ((type = source as Type) != null)
            {
                return type.Name;
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
