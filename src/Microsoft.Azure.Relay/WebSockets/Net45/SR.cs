//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay.WebSockets
{
    using System.Globalization;

    class SR : Strings
    {
        public static string GetString(string format, params object[] args)
        {
            if (args != null && args.Length != 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string text = args[i] as string;
                    if (text != null && text.Length > 1024)
                    {
                        args[i] = text.Substring(0, 1021) + "...";
                    }
                }

                return string.Format(CultureInfo.CurrentUICulture, format, args);
            }

            return format;
        }
    }
}