// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Net;
    using System.Runtime.CompilerServices;

    interface ITraceSource
    {
        TrackingContext TrackingContext { get; }
    }

    /// <summary>
    /// EventSource for the new Dynamic EventSource type of Microsoft-Azure-Relay traces.
    /// 
    /// The default Level is Informational
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

        [NonEvent]
        public void ObjectConnecting(object source)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                this.ObjectConnecting(details.Source);
            }
        }

        [NonEvent]
        public void ObjectConnecting(string source, TrackingContext trackingContext)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.ObjectConnecting(source);
            }
        }

        [Event(40200, Level = EventLevel.Informational, Message = "{0}: Connecting.")]
        void ObjectConnecting(string source)
        {
            this.WriteEvent(40200, source);
        }

        [NonEvent]
        public void ObjectConnected(object source)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                this.ObjectConnected(details.Source);
            }
        }

        [NonEvent]
        public void ObjectConnected(string source, TrackingContext trackingContext)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.ObjectConnected(source);
            }
        }

        [Event(40201, Level = EventLevel.Informational, Message = "{0}: Connected")]
        void ObjectConnected(string source)
        {
            this.WriteEvent(40201, source);
        }

        [NonEvent]
        public void ObjectClosing(object source)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                this.ObjectClosing(details.Source);
            }
        }

        [Event(40202, Level = EventLevel.Informational, Message = "{0}: is Closing")]
        void ObjectClosing(string source)
        {
            this.WriteEvent(40202, source);
        }

        [NonEvent]
        public void ObjectClosed(object source)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                this.ObjectClosed(details.Source);
            }
        }

        [Event(40203, Level = EventLevel.Informational, Message = "{0}: is Closed.")]
        void ObjectClosed(string source)
        {
            this.WriteEvent(40203, source);
        }

        [NonEvent]
        public void RelayListenerRendezvousStart(object source, string trackingId, string rendezvousAddress)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.RelayListenerRendezvousStart(details.Source, trackingId, rendezvousAddress);
            }
        }

        [Event(40204, Message = "{0}: Relay Listener Received a connection request. ConnectionId: {1}, Rendezvous Address: {2}.")]
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
        public void RelayListenerRendezvousFailed(object source, string trackingId, Exception exception)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.RelayListenerRendezvousFailed(details.Source, trackingId, ExceptionToString(exception, details.TrackingContext));
            }
        }

        [NonEvent]
        public void RelayListenerRendezvousFailed(object source, string trackingId, string error)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.RelayListenerRendezvousFailed(details.Source, trackingId, error);
            }
        }

        [Event(40206, Level = EventLevel.Warning, Message = "{0}: Relayed Listener failed to accept client. ConnectionId: {1}, Exception: {2}.")]
        void RelayListenerRendezvousFailed(string source, string clientId, string exception)
        {
            this.WriteEvent(40206, source, clientId, exception);
        }

        [NonEvent]
        public void RelayListenerRendezvousRejected(TrackingContext trackingContext, HttpStatusCode statusCode, string statusDescription)
        {
            if (this.IsEnabled())
            {
                SetCurrentThreadActivityId(trackingContext);
                this.RelayListenerRendezvousRejected(trackingContext.ToString(), (int)statusCode, statusDescription);
            }
        }

        [Event(40207, Level = EventLevel.Warning, Message = "Relayed Listener is rejecting the client. ConnectionId: {0}, StatusCode: {1}, StatusDescription: {2}.")]
        public void RelayListenerRendezvousRejected(string connectionId, int statusCode, string statusDescription)
        {
            this.WriteEvent(40207, connectionId, statusCode, statusDescription);
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsInformation(object source, Exception exception, [CallerMemberName] string member = "")
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.HandledExceptionAsInformation(details.Source, member, ExceptionToString(exception, details.TrackingContext));
            }
        }

        [Event(40249, Message = "{0}.{1} Handled Exception: {2}")]
        void HandledExceptionAsInformation(string source, string member, string exception)
        {
            this.WriteEvent(40249, source, member, exception);
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsWarning(object source, Exception exception, [CallerMemberName] string member = "")
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.HandledExceptionAsWarning(details.Source, member, ExceptionToString(exception, details.TrackingContext));
            }
        }

        [Event(40250, Level = EventLevel.Warning, Message = "{0}.{1} Handled Exception: {2}")]
        void HandledExceptionAsWarning(string source, string member, string exception)
        {
            this.WriteEvent(40250, source, member, exception);
        }

        // Not the actual event definition since we're using object and Exception types
        [NonEvent]
        public void HandledExceptionAsError(object source, Exception exception, [CallerMemberName] string member = "")
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.HandledExceptionAsError(details.Source, member, ExceptionToString(exception, details.TrackingContext));
            }
        }

        [Event(40251, Level = EventLevel.Error, Message = "{0}.{1} Handled Exception {2}")]
        void HandledExceptionAsError(string source, string member, string exception)
        {
            this.WriteEvent(40251, source, member, exception);
        }

        [NonEvent]
        public void GetTokenStart(object source)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.GetTokenStart(details.Source);
            }
        }

        [Event(40255, Level = EventLevel.Informational, Message = "{0}: GetToken start")]
        void GetTokenStart(string source)
        {
            this.WriteEvent(40255, source);
        }

        [NonEvent]
        public void GetTokenStop(object source, DateTime tokenExpiry)
        {
            if (this.IsEnabled())
            {
                PrepareTrace(source);
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
                var details = PrepareTrace(source);
                this.TokenRenewScheduled(interval.ToString(), details.Source);
            }
        }

        [Event(40257, Level = EventLevel.Informational, Message = "{1}: Scheduling Token renewal after {0}.")]
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
        public TException ThrowingException<TException>(TException exception, object source = null, EventLevel level = EventLevel.Error, [CallerMemberName] string memberName = "")
            where TException : Exception
        {
            // Avoid converting ToString, etc. if ETW tracing is not enabled.
            if (this.IsEnabled(level, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                string exceptionString = ExceptionToString(exception, details.TrackingContext);
                switch (level)
                {
                    case EventLevel.Critical:
                    case EventLevel.LogAlways:
                    case EventLevel.Error:
                        this.ThrowingExceptionError(details.Source, exceptionString, memberName);
                        break;
                    case EventLevel.Warning:
                        this.ThrowingExceptionWarning(details.Source, exceptionString, memberName);
                        break;
                    case EventLevel.Informational:
                    case EventLevel.Verbose:
                    default:
                        this.ThrowingExceptionInfo(details.Source, exceptionString, memberName);
                        break;
                }
            }

            // This allows "throw ServiceBusEventSource.Log.ThrowingException(..."
            return exception;
        }

        [Event(40262, Level = EventLevel.Error, Message = "{0}.{2}: Throwing an Exception: {1}")]
        void ThrowingExceptionError(string source, string exception, string memberName)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40262, source, exception, memberName);
        }

        [Event(40263, Level = EventLevel.Warning, Message = "{0}.{2}: Throwing an Exception: {1}")]
        void ThrowingExceptionWarning(string source, string exception, string memberName)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40263, source, exception, memberName);
        }

        [Event(40264, Level = EventLevel.Informational, Message = "{0}.{2}: Throwing an Exception: {1}")]
        void ThrowingExceptionInfo(string source, string exception, string memberName)
        {
            // The IsEnabled() check is in the [NonEvent] Wrapper method
            this.WriteEvent(40264, source, exception, memberName);
        }

        [NonEvent]
        public void Warning(object source, string message)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                if (details.TrackingContext != null)
                {
                    message += " " + details.TrackingContext.ToString();
                }

                this.Warning(details.Source, message);
            }
        }

        [Event(40303, Level = EventLevel.Informational, Message = "{0}: {1}")]
        void Warning(string source, string details)
        {
            this.WriteEvent(40303, source, details);
        }

        [NonEvent]
        public void Info(object source, string message)
        {
            if (this.IsEnabled())
            {
                var details = PrepareTrace(source);
                this.Info(details.Source, message);
            }
        }

        [NonEvent]
        public void Info(string source, TrackingContext trackingContext, string message)
        {
            if (this.IsEnabled())
            {
                SetCurrentThreadActivityId(trackingContext);
                this.Info(source, message);
            }
        }

        [Event(40304, Level = EventLevel.Informational, Message = "{0}: {1}")]
        void Info(string source, string details)
        {
            this.WriteEvent(40304, source, details);
        }

        [NonEvent]
        public void HybridHttpResponseStreamWrite(TrackingContext trackingContext, int count)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpResponseStreamWrite(count);
            }
        }

        [Event(40306, Level = EventLevel.Verbose, Message = "HybridHttpConnection+ResponseStream: WriteAsync(count={0})")]
        void HybridHttpResponseStreamWrite(int count)
        {
            this.WriteEvent(40306, count);
        }

        [NonEvent]
        public void HybridHttpResponseStreamFlush(TrackingContext trackingContext, string reason)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpResponseStreamFlush(reason);
            }
        }

        [Event(40307, Level = EventLevel.Verbose, Message = "HybridHttpConnection+ResponseStream: FlushCoreAsync(reason={0})")]
        void HybridHttpResponseStreamFlush(string reason)
        {
            this.WriteEvent(40307, reason);
        }

        [NonEvent]
        public void HybridHttpConnectionSendBytes(TrackingContext trackingContext, int count)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpConnectionSendBytes(count);
            }
        }

        [Event(40308, Level = EventLevel.Verbose, Message = "HybridHttpConnection: Sending {0} bytes on the rendezvous connection")]
        void HybridHttpConnectionSendBytes(int count)
        {
            this.WriteEvent(40308, count);
        }

        [NonEvent]
        public void HybridHttpCreatingRendezvousConnection(TrackingContext trackingContext)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpCreatingRendezvousConnection();
            }
        }

        [Event(40309, Level = EventLevel.Informational, Message = "HybridHttpConnection: Creating the rendezvous connection")]
        void HybridHttpCreatingRendezvousConnection()
        {
            this.WriteEvent(40309);
        }

        [NonEvent]
        public void HybridHttpConnectionSendResponse(TrackingContext trackingContext, string connection, int status)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpConnectionSendResponse(connection, status);
            }
        }

        [Event(40310, Level = EventLevel.Informational, Message = "HybridHttpConnection: Sending the response command on the {0} connection, status: {1}")]
        void HybridHttpConnectionSendResponse(string connection, int status)
        {
            this.WriteEvent(40310, connection, status);
        }

        [NonEvent]
        public void HybridHttpReadRendezvousValue(object source, string value)
        {
            if (this.IsEnabled(EventLevel.Verbose, EventKeywords.None))
            {
                var details = PrepareTrace(source);
                this.HybridHttpReadRendezvousValue(value);
            }
        }

        [Event(40313, Level = EventLevel.Verbose, Message = "HybridHttpConnection: Reading {0} from the rendezvous connection")]
        void HybridHttpReadRendezvousValue(string value)
        {
            this.WriteEvent(40313, value);
        }

        [NonEvent]
        public void HybridHttpRequestStarting(TrackingContext trackingContext)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpRequestStarting(trackingContext?.ToString() ?? string.Empty);
            }
        }

        [Event(40314, Level = EventLevel.Informational, Message = "HybridHttpConnection: request initializing. {0}")]
        void HybridHttpRequestStarting(string details)
        {
            this.WriteEvent(40314, details);
        }

        [NonEvent]
        public void HybridHttpRequestReceived(TrackingContext trackingContext, string method)
        {
            if (this.IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                SetCurrentThreadActivityId(trackingContext);
                this.HybridHttpRequestReceived(method, trackingContext?.Address ?? string.Empty);
            }
        }

        [Event(40315, Level = EventLevel.Informational, Message = "HybridHttpConnection: Request: {0} {1}")]
        void HybridHttpRequestReceived(string method, string address)
        {
            this.WriteEvent(40315, method, address);
        }

        [Event(40316, Level = EventLevel.Informational, Message = "HybridHttpConnection: Invoking user RequestHandler")]
        public void HybridHttpInvokingUserRequestHandler()
        {
            this.WriteEvent(40316);
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
        static string ExceptionToString(Exception exception, TrackingContext trackingContext = null)
        {
            string exceptionString = exception?.ToString() ?? string.Empty;
            if (trackingContext != null)
            {
                exceptionString = $"{trackingContext}: {exceptionString}";
            }

            return exceptionString;
        }

        [NonEvent]
        static string DateTimeToString(DateTime dateTime)
        {
            return dateTime.ToString(CultureInfo.InvariantCulture);
        }

        static TraceDetails PrepareTrace(object source)
        {
            string sourceString;
            TrackingContext trackingContext;

            var traceSource = source as ITraceSource;
            if (traceSource != null)
            {
                trackingContext = traceSource.TrackingContext;
                SetCurrentThreadActivityId(trackingContext);
                sourceString = traceSource.ToString();
            }
            else
            {
                sourceString = CreateSourceString(source);
                trackingContext = null;
            }

            return new TraceDetails(sourceString, trackingContext);
        }

        static void SetCurrentThreadActivityId(TrackingContext trackingContext)
        {
            if (trackingContext != null)
            {
                SetCurrentThreadActivityId(trackingContext.ActivityId);
            }
        }

        public class Keywords   // This is a bitvector
        {
            //public const EventKeywords Client = (EventKeywords)0x0001;
            //public const EventKeywords Relay = (EventKeywords)0x0002;
            //public const EventKeywords Messaging = (EventKeywords)0x0002;
        }

        struct TraceDetails
        {
            public string Source;
            public TrackingContext TrackingContext;

            public TraceDetails(string source, TrackingContext trackingContext)
            {
                this.Source = source;
                this.TrackingContext = trackingContext;
            }
        }
    }
}
