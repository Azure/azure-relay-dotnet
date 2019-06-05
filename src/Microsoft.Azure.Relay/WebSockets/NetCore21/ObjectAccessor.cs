// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets.NetCore21
{
    using System;
    using System.Reflection;
    using Microsoft.Azure.Relay;

    abstract class ObjectAccessor
    {
        readonly Type instanceType;

        public ObjectAccessor(object instance)
        {
            this.Instance = instance ?? throw RelayEventSource.Log.ArgumentNull(nameof(instance), this);
            this.instanceType = instance.GetType();
        }

        protected object Instance { get; }

        protected void SetProperty(string propertyName, object value)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(propertyName), this);
            }

            PropertyInfo property = this.instanceType.GetProperty(propertyName);
            property.GetSetMethod(true).Invoke(this.Instance, new[] { value });
        }

        protected T GetProperty<T>(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(propertyName), this);
            }

            PropertyInfo property = this.instanceType.GetProperty(propertyName);
            return (T)property.GetGetMethod(true).Invoke(this.Instance, null);
        }
    }
}