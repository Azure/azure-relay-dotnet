//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Creates and manages the contents of connection strings. You can use this class to construct a connection string 
    /// for working with a Relay namespace. It can also be used to perform basic validation on an existing connection string.
    /// </summary> 
    public class RelayConnectionStringBuilder
    {
        const string EndpointConfigName = "Endpoint";
        const string EntityPathConfigName = "EntityPath";
        const string OperationTimeoutConfigName = "OperationTimeout";
        const string SharedAccessKeyNameConfigName = "SharedAccessKeyName";
        const string SharedAccessKeyConfigName = "SharedAccessKey";
        const string SharedAccessSignatureConfigName = "SharedAccessSignature";
        const char KeyValueSeparator = '=';
        const char KeyValuePairDelimiter = ';';
        Uri endpoint;
        TimeSpan operationTimeout;

        /// <summary>Initializes a new instance of the <see cref="RelayConnectionStringBuilder" /> class.</summary>
        public RelayConnectionStringBuilder()
        {
            this.OperationTimeout = RelayConstants.DefaultOperationTimeout;
        }

        /// <summary>
        /// Initializes a new instance of a <see cref="RelayConnectionStringBuilder" /> with a specified existing connection string.
        /// </summary> 
        /// <param name="connectionString">The connection string, which can be obtained from the Azure Management Portal.</param>
        /// <exception cref="ArgumentNullException">Thrown if connectionString is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <see cref="RelayConnectionStringBuilder.OperationTimeout"/> is a non-positive <see cref="TimeSpan"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown if a key value pair is missing either a key or a value.
        /// Thrown if <see cref="RelayConnectionStringBuilder.Endpoint"/> is specified but is not a valid absolute <see cref="Uri"/>.
        /// Thrown if <see cref="RelayConnectionStringBuilder.OperationTimeout"/> is specified but is not a valid <see cref="TimeSpan"/> format.
        /// Thrown if an unsupported key name is specified.
        /// </exception>
        public RelayConnectionStringBuilder(string connectionString)
            : this()
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(connectionString), this);
            }

            this.ParseConnectionString(connectionString);
        }

        /// <summary>Gets or sets the Relay namespace address.</summary>
        /// <exception cref="ArgumentNullException">Thrown if Endpoint is being set to null.</exception>
        /// <exception cref="ArgumentException">Thrown if Endpoint is being set to a <see cref="Uri"/>  which is not absolute.</exception>
        public Uri Endpoint
        {
            get
            {
                return this.endpoint;
            }
            set
            {
                if (value == null)
                {
                    throw RelayEventSource.Log.ArgumentNull(nameof(Endpoint), this);
                }
                else if (!value.IsAbsoluteUri)
                {
                    throw RelayEventSource.Log.Argument(nameof(Endpoint), SR.GetString(SR.NotValidAbsoluteUri, nameof(Endpoint)));
                }

                this.endpoint = value;
            }
        }

        /// <summary>Gets or sets the <see cref="System.TimeSpan" /> that specifies how long the operation has to complete before timing out.</summary> 
        /// <value>The <see cref="System.TimeSpan" /> that specifies how long the operation has to complete before timing out.
        /// The default value is one minute.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if OperationTimeout is set to a non-positive TimeSpan.</exception>
        public TimeSpan OperationTimeout
        {
            get
            {
                return this.operationTimeout;
            }
            set
            {
                TimeoutHelper.ThrowIfNonPositiveArgument(value, nameof(OperationTimeout));
                this.operationTimeout = value;
            }
        }

        /// <summary>Gets or sets the entity path for the HybridConnection.</summary> 
        /// <value>Returns the entity path.</value>
        public string EntityPath { get; set; }

        /// <summary>Gets or sets the name of the shared access key.</summary>
        /// <value>The name of the shared access key.</value>
        public string SharedAccessKeyName { get; set; }

        /// <summary>Gets or sets the shared access key for the connection authentication.</summary>
        /// <value>The shared access key for the connection authentication.</value>
        public string SharedAccessKey { get; set; }

        /// <summary>Gets or sets the SAS token.</summary>
        /// <value>Returns the configured SAS token.</value>
        public string SharedAccessSignature { get; set; }

        /// <summary>Creates a connectionString that represents the current object.</summary>
        /// <returns>A connectionString that represents the current object.</returns>
        public override string ToString()
        {
            this.Validate();
            var connectionStringBuilder = new StringBuilder();

            // Endpoint is Required (Validate throws if not present)
            connectionStringBuilder.Append($"{EndpointConfigName}{KeyValueSeparator}{this.Endpoint.AbsoluteUri}{KeyValuePairDelimiter}");

            if (!string.IsNullOrWhiteSpace(this.EntityPath))
            {
                connectionStringBuilder.Append($"{EntityPathConfigName}{KeyValueSeparator}{this.EntityPath}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.SharedAccessKeyName))
            {
                connectionStringBuilder.Append($"{SharedAccessKeyNameConfigName}{KeyValueSeparator}{this.SharedAccessKeyName}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.SharedAccessKey))
            {
                connectionStringBuilder.Append($"{SharedAccessKeyConfigName}{KeyValueSeparator}{this.SharedAccessKey}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.SharedAccessSignature))
            {
                connectionStringBuilder.Append($"{SharedAccessSignatureConfigName}{KeyValueSeparator}{this.SharedAccessSignature}{KeyValuePairDelimiter}");
            }

            if (this.OperationTimeout != RelayConstants.DefaultOperationTimeout)
            {
                connectionStringBuilder.Append($"{OperationTimeoutConfigName}{KeyValueSeparator}{this.OperationTimeout.ToString(null, CultureInfo.InvariantCulture)}{KeyValuePairDelimiter}");
            }

            return connectionStringBuilder.ToString();
        }

        internal TokenProvider CreateTokenProvider()
        {
            TokenProvider tokenProvider = null;
            if (!string.IsNullOrEmpty(this.SharedAccessSignature))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(this.SharedAccessSignature);
            }
            else if (!string.IsNullOrEmpty(this.SharedAccessKeyName) && !string.IsNullOrEmpty(this.SharedAccessKey))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(this.SharedAccessKeyName, this.SharedAccessKey);
            }
            else
            {
                throw RelayEventSource.Log.Argument("connectionString", SR.ConnectionStringMustIncludeTokenProviderSettings);
            }

            return tokenProvider;
        }

        internal void Validate()
        {
            if (this.Endpoint == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(Endpoint));
            }

            // if one supplied SharedAccessKeyName, they need to supply SharedAccessKey, and vise versa
            // if SharedAccessSignature is specified, Neither SasKey nor SasKeyName should not be specified
            var hasSharedAccessKeyName = !string.IsNullOrWhiteSpace(this.SharedAccessKeyName);
            var hasSharedAccessKey = !string.IsNullOrWhiteSpace(this.SharedAccessKey);

            var hasSharedAccessSignature = !string.IsNullOrWhiteSpace(this.SharedAccessSignature);
            if (hasSharedAccessSignature)
            {
                if (hasSharedAccessKeyName)
                {
                    throw RelayEventSource.Log.Argument(
                        SharedAccessSignatureConfigName + "," + SharedAccessKeyNameConfigName,
                        SR.GetString(SR.SasTokenShouldBeAlone, SharedAccessSignatureConfigName, SharedAccessKeyNameConfigName));
                }

                if (hasSharedAccessKey)
                {
                    throw RelayEventSource.Log.Argument(
                        SharedAccessSignatureConfigName + "," + SharedAccessKeyConfigName,
                        SR.GetString(SR.SasTokenShouldBeAlone, SharedAccessSignatureConfigName, SharedAccessKeyConfigName));
                }
            }
            else if ((hasSharedAccessKeyName && !hasSharedAccessKey) || (!hasSharedAccessKeyName && hasSharedAccessKey))
            {
                // SharedAccessKeyName + SharedAccessKey go together, cannot specify one without the other.
                throw RelayEventSource.Log.Argument(SharedAccessKeyNameConfigName + "," + SharedAccessKeyConfigName,
                    SR.GetString(SR.ArgumentInvalidCombination, SharedAccessKeyNameConfigName + "," + SharedAccessKeyConfigName));
            }
        }

        void ParseConnectionString(string connectionString)
        {
            // First split into strings based on ';'
            string[] keyValuePairs = connectionString.Split(new[] { KeyValuePairDelimiter }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyValuePair in keyValuePairs)
            {
                // Now split based on the *first* '='
                string[] keyAndValue = keyValuePair.Split(new[] { KeyValueSeparator }, 2);
                string key = keyAndValue[0];
                if (keyAndValue.Length != 2)
                {
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringParameterValueMissing, key));
                }

                string value = keyAndValue[1];
                if (key.Equals(EndpointConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    Uri endpoint;
                    if (!Uri.TryCreate(value, UriKind.Absolute, out endpoint))
                    {
                        throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.NotValidAbsoluteUri, EndpointConfigName));
                    }

                    this.Endpoint = endpoint;
                }
                else if (key.Equals(EntityPathConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.EntityPath = value;
                }
                else if (key.Equals(SharedAccessKeyNameConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.SharedAccessKeyName = value;
                }
                else if (key.Equals(SharedAccessKeyConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.SharedAccessKey = value;
                }
                else if (key.Equals(SharedAccessSignatureConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.SharedAccessSignature = value;
                }
                else if (key.Equals(OperationTimeoutConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    TimeSpan timeValue;
                    if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out timeValue))
                    {
                        throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.NotValidTimeSpan, OperationTimeoutConfigName));
                    }

                    this.OperationTimeout = timeValue;
                }
                else
                {
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringUnknownParameter, key));
                }
            }
        }
    }
}
