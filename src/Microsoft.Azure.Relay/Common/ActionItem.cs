// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Threading.Tasks;

    static class ActionItem
    {
        public static void Schedule(Action<object> action, object state)
        {
            // UWP doesn't support ThreadPool[.QueueUserWorkItem] so use Task.Factory.StartNew
            Task.Factory.StartNew(s => action(s), state, TaskCreationOptions.DenyChildAttach);
        }
    }
}
