// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets
{
    using System;
    using System.Threading;
    using System.Diagnostics;
    using System.Security;
    using System.Globalization;
    using System.Diagnostics.CodeAnalysis;
    
    // From: https://referencesource.microsoft.com/#System/net/System/Net/Logging.cs
    internal class Logging
    {
        private static volatile bool s_LoggingEnabled = true;
        private static volatile bool s_LoggingInitialized;
        private static volatile bool s_AppDomainShutdown;

        private const int DefaultMaxDumpSize = 1024;
        private const bool DefaultUseProtocolTextOnly = false;

        private const string AttributeNameMaxSize = "maxdatasize";
        private const string AttributeNameTraceMode = "tracemode";
        private static readonly string[] SupportedAttributes = new string[] { AttributeNameMaxSize, AttributeNameTraceMode };

        private const string AttributeValueProtocolOnly = "protocolonly";
        //private const string AttributeValueIncludeHex = "includehex";

        private const string TraceSourceWebSocketsName = " Microsoft.ServiceBus.Relay.WebSockets";

        private static TraceSource s_WebSocketsTraceSource;

        private Logging()
        {
        }

        private static object s_InternalSyncObject;

        private static object InternalSyncObject
        {
            get
            {
                if (s_InternalSyncObject == null)
                {
                    object o = new Object();
                    Interlocked.CompareExchange(ref s_InternalSyncObject, o, null);
                }
                return s_InternalSyncObject;
            }
        }

        internal static bool On
        {
            get
            {
                if (!s_LoggingInitialized)
                {
                    InitializeLogging();
                }
                return s_LoggingEnabled;
            }
        }

        internal static TraceSource WebSockets
        {
            get
            {
                if (!s_LoggingInitialized)
                {
                    InitializeLogging();
                }
                if (!s_LoggingEnabled)
                {
                    return null;
                }
                return s_WebSocketsTraceSource;
            }
        }

        private static bool GetUseProtocolTextSetting(TraceSource traceSource)
        {
            bool useProtocolTextOnly = DefaultUseProtocolTextOnly;
            if (traceSource.Attributes[AttributeNameTraceMode] == AttributeValueProtocolOnly)
            {
                useProtocolTextOnly = true;
            }
            return useProtocolTextOnly;
        }

        private static int GetMaxDumpSizeSetting(TraceSource traceSource)
        {
            int maxDumpSize = DefaultMaxDumpSize;
            if (traceSource.Attributes.ContainsKey(AttributeNameMaxSize))
            {
                try
                {
                    maxDumpSize = Int32.Parse(traceSource.Attributes[AttributeNameMaxSize], NumberFormatInfo.InvariantInfo);
                }
                catch (Exception exception)
                {
                    if (exception is ThreadAbortException || exception is StackOverflowException || exception is OutOfMemoryException)
                    {
                        throw;
                    }
                    traceSource.Attributes[AttributeNameMaxSize] = maxDumpSize.ToString(NumberFormatInfo.InvariantInfo);
                }
            }
            return maxDumpSize;
        }

        /// <devdoc>
        ///    <para>Sets up internal config settings for logging. (MUST be called under critsec) </para>
        /// </devdoc>
        private static void InitializeLogging()
        {
            lock (InternalSyncObject)
            {
                if (!s_LoggingInitialized)
                {
                    bool loggingEnabled = false;
                    s_WebSocketsTraceSource = new NclTraceSource(TraceSourceWebSocketsName);

                    //GlobalLog.Print("Initalizating tracing");

                    try
                    {
                        loggingEnabled = s_WebSocketsTraceSource.Switch.ShouldTrace(TraceEventType.Critical);
                    }
                    catch (SecurityException)
                    {
                        // These may throw if the caller does not have permission to hook up trace listeners.
                        // We treat this case as though logging were disabled.
                        Close();
                        loggingEnabled = false;
                    }
                    if (loggingEnabled)
                    {
                        AppDomain currentDomain = AppDomain.CurrentDomain;
                        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);
                        currentDomain.DomainUnload += new EventHandler(AppDomainUnloadEvent);
                        currentDomain.ProcessExit += new EventHandler(ProcessExitEvent);
                    }
                    s_LoggingEnabled = loggingEnabled;
                    s_LoggingInitialized = true;
                }
            }
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Logging functions must work in partial trust mode")]
        private static void Close()
        {
            if (s_WebSocketsTraceSource != null) s_WebSocketsTraceSource.Close();
        }

        /// <devdoc>
        ///    <para>Logs any unhandled exception through this event handler</para>
        /// </devdoc>
        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Exception(WebSockets, sender, "UnhandledExceptionHandler", e);
        }

        private static void ProcessExitEvent(object sender, EventArgs e)
        {
            Close();
            s_AppDomainShutdown = true;
        }
        /// <devdoc>
        ///    <para>Called when the system is shutting down, used to prevent additional logging post-shutdown</para>
        /// </devdoc>
        private static void AppDomainUnloadEvent(object sender, EventArgs e)
        {
            Close();
            s_AppDomainShutdown = true;
        }

        /// <devdoc>
        ///    <para>Confirms logging is enabled, given current logging settings</para>
        /// </devdoc>
        private static bool ValidateSettings(TraceSource traceSource, TraceEventType traceLevel)
        {
            if (!s_LoggingEnabled)
            {
                return false;
            }
            if (!s_LoggingInitialized)
            {
                InitializeLogging();
            }
            if (traceSource == null || !traceSource.Switch.ShouldTrace(traceLevel))
            {
                return false;
            }
            if (s_AppDomainShutdown)
            {
                return false;
            }
            return true;
        }

        /// <devdoc>
        ///    <para>Converts an object to a normalized string that can be printed
        ///         takes System.Net.ObjectNamedFoo and coverts to ObjectNamedFoo, 
        ///         except IPAddress, IPEndPoint, and Uri, which return ToString()
        ///         </para>
        /// </devdoc>
        private static string GetObjectName(object obj)
        {
            if (obj is Uri || obj is System.Net.IPAddress || obj is System.Net.IPEndPoint)
            {
                return obj.ToString();
            }
            else
            {
                return obj.GetType().Name;
            }
        }

        internal static uint GetThreadId()
        {
            // Note: Changed from equivalent Thread.CurrentThread.GetHashCode() to support UWP
            uint threadId = (uint)Environment.CurrentManagedThreadId;
            return threadId;
        }

        internal static void PrintLine(TraceSource traceSource, TraceEventType eventType, int id, string msg)
        {
            string logHeader = "[" + GetThreadId().ToString("d4", CultureInfo.InvariantCulture) + "] ";
            traceSource.TraceEvent(eventType, id, logHeader + msg);
        }

        /// <devdoc>
        ///    <para>Indicates that two objects are getting used with one another</para>
        /// </devdoc>
        internal static void Associate(TraceSource traceSource, object objA, object objB)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }

            string lineA = GetObjectName(objA) + "#" + HashString(objA);
            string lineB = GetObjectName(objB) + "#" + HashString(objB);

            PrintLine(traceSource, TraceEventType.Information, 0, "Associating " + lineA + " with " + lineB);
        }

        /// <devdoc>
        ///    <para>Logs entrance to a function</para>
        /// </devdoc>
        internal static void Enter(TraceSource traceSource, object obj, string method, string param)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            Enter(traceSource, GetObjectName(obj) + "#" + HashString(obj), method, param);
        }

        /// <devdoc>
        ///    <para>Logs entrance to a function</para>
        /// </devdoc>
        internal static void Enter(TraceSource traceSource, string obj, string method, string param)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            Enter(traceSource, obj + "::" + method + "(" + param + ")");
        }

        /// <devdoc>
        ///    <para>Logs entrance to a function, indents and points that out</para>
        /// </devdoc>
        internal static void Enter(TraceSource traceSource, string msg)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            // Trace.CorrelationManager.StartLogicalOperation();
            PrintLine(traceSource, TraceEventType.Verbose, 0, msg);
        }

        /// <devdoc>
        ///    <para>Logs exit from a function</para>
        /// </devdoc>
        internal static void Exit(TraceSource traceSource, object obj, string method, string retValue)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            Exit(traceSource, GetObjectName(obj) + "#" + HashString(obj), method, retValue);
        }

        /// <devdoc>
        ///    <para>Logs exit from a function</para>
        /// </devdoc>
        internal static void Exit(TraceSource traceSource, string obj, string method, string retValue)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            if (!IsBlankString(retValue))
            {
                retValue = "\t-> " + retValue;
            }
            Exit(traceSource, obj + "::" + method + "() " + retValue);
        }

        /// <devdoc>
        ///    <para>Logs exit from a function</para>
        /// </devdoc>
        internal static void Exit(TraceSource traceSource, string msg)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            PrintLine(traceSource, TraceEventType.Verbose, 0, "Exiting " + msg);
            // Trace.CorrelationManager.StopLogicalOperation();
        }

        /// <devdoc>
        ///    <para>Logs Exception, restores indenting</para>
        /// </devdoc>
        internal static void Exception(TraceSource traceSource, object obj, string method, Exception e)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Error))
            {
                return;
            }

            string infoLine = SR.GetString(SR.net_log_exception, GetObjectLogHash(obj), method, e.Message);
            if (!IsBlankString(e.StackTrace))
            {
                infoLine += "\r\n" + e.StackTrace;
            }
            PrintLine(traceSource, TraceEventType.Error, 0, infoLine);
        }

        /// <devdoc>
        ///    <para>Logs an Info line</para>
        /// </devdoc>
        internal static void PrintInfo(TraceSource traceSource, object obj, string method, string param)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Information))
            {
                return;
            }
            PrintLine(traceSource, TraceEventType.Information, 0,
                                   GetObjectName(obj) + "#" + HashString(obj)
                                   + "::" + method + "(" + param + ")");
        }

        /// <devdoc>
        ///    <para>Logs a Warning line</para>
        /// </devdoc>
        internal static void PrintWarning(TraceSource traceSource, object obj, string method, string msg)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Warning))
            {
                return;
            }
            PrintLine(traceSource, TraceEventType.Warning, 0,
                                   GetObjectName(obj) + "#" + HashString(obj)
                                   + "::" + method + "() - " + msg);
        }

        internal static string GetObjectLogHash(object obj)
        {
            return GetObjectName(obj) + "#" + HashString(obj);
        }

        /// <devdoc>
        ///    <para>Dumps a byte array to the log</para>
        /// </devdoc>
        internal static void Dump(TraceSource traceSource, object obj, string method, byte[] buffer, int offset, int length)
        {
            if (!ValidateSettings(traceSource, TraceEventType.Verbose))
            {
                return;
            }
            if (buffer == null)
            {
                PrintLine(traceSource, TraceEventType.Verbose, 0, "(null)");
                return;
            }
            if (offset > buffer.Length)
            {
                PrintLine(traceSource, TraceEventType.Verbose, 0, "(offset out of range)");
                return;
            }
            PrintLine(traceSource, TraceEventType.Verbose, 0, "Data from " + GetObjectName(obj) + "#" + HashString(obj) + "::" + method);
            int maxDumpSize = GetMaxDumpSizeSetting(traceSource);
            if (length > maxDumpSize)
            {
                PrintLine(traceSource, TraceEventType.Verbose, 0, "(printing " + maxDumpSize.ToString(NumberFormatInfo.InvariantInfo) + " out of " + length.ToString(NumberFormatInfo.InvariantInfo) + ")");
                length = maxDumpSize;
            }
            if ((length < 0) || (length > buffer.Length - offset))
            {
                length = buffer.Length - offset;
            }
            if (GetUseProtocolTextSetting(traceSource))
            {
                string output = "<<" + HeaderEncoding.GetString(buffer, offset, length) + ">>";
                PrintLine(traceSource, TraceEventType.Verbose, 0, output);
                return;
            }
            do
            {
                int n = Math.Min(length, 16);
                string disp = String.Format(CultureInfo.CurrentCulture, "{0:X8} : ", offset);
                for (int i = 0; i < n; ++i)
                {
                    disp += String.Format(CultureInfo.CurrentCulture, "{0:X2}", buffer[offset + i]) + ((i == 7) ? '-' : ' ');
                }
                for (int i = n; i < 16; ++i)
                {
                    disp += "   ";
                }
                disp += ": ";
                for (int i = 0; i < n; ++i)
                {
                    disp += ((buffer[offset + i] < 0x20) || (buffer[offset + i] > 0x7e))
                                ? '.'
                                : (char)(buffer[offset + i]);
                }
                PrintLine(traceSource, TraceEventType.Verbose, 0, disp);
                offset += n;
                length -= n;
            } while (length > 0);
        }

        internal static string HashString(object objectValue)
        {
            if (objectValue == null)
            {
                return "(null)";
            }
            if (objectValue is string && ((string)objectValue).Length == 0)
            {
                return "(string.empty)";
            }
            return objectValue.GetHashCode().ToString(NumberFormatInfo.InvariantInfo);
        }

        internal static bool IsBlankString(string stringValue)
        {
            return stringValue == null || stringValue.Length == 0;
        }

        private class NclTraceSource : TraceSource
        {
            internal NclTraceSource(string name) : base(name) { }

            protected override string[] GetSupportedAttributes()
            {
                return Logging.SupportedAttributes;
            }
        }

        // we use this static class as a helper class to encode/decode HTTP headers.
        // what we need is a 1-1 correspondence between a char in the range U+0000-U+00FF
        // and a byte in the range 0x00-0xFF (which is the range that can hit the network).
        // The Latin-1 encoding (ISO-88591-1) (GetEncoding(28591)) works for byte[] to string, but is a little slow.
        // It doesn't work for string -> byte[] because of best-fit-mapping problems.
        internal static class HeaderEncoding
        {
            internal static unsafe string GetString(byte[] bytes, int byteIndex, int byteCount)
            {
                fixed (byte* pBytes = bytes)
                    return GetString(pBytes + byteIndex, byteCount);
            }

            internal static unsafe string GetString(byte* pBytes, int byteCount)
            {
                if (byteCount < 1)
                    return "";

                string s = new String('\0', byteCount);

                fixed (char* pStr = s)
                {
                    char* pString = pStr;
                    while (byteCount >= 8)
                    {
                        pString[0] = (char)pBytes[0];
                        pString[1] = (char)pBytes[1];
                        pString[2] = (char)pBytes[2];
                        pString[3] = (char)pBytes[3];
                        pString[4] = (char)pBytes[4];
                        pString[5] = (char)pBytes[5];
                        pString[6] = (char)pBytes[6];
                        pString[7] = (char)pBytes[7];
                        pString += 8;
                        pBytes += 8;
                        byteCount -= 8;
                    }
                    for (int i = 0; i < byteCount; i++)
                    {
                        pString[i] = (char)pBytes[i];
                    }
                }

                return s;
            }
        }
    }
}
