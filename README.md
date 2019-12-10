<p align="center">
  <img src="relay.png" alt="Microsoft Azure Relay" width="100"/>
</p>

# Microsoft Azure Relay Hybrid Connections Client for .NET

|Build/Package|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/qhefoxrdg068xjhj/branch/master?svg=true)](https://ci.appveyor.com/project/jtaubensee/azure-relay-dotnet/branch/master) [![codecov](https://codecov.io/gh/Azure/azure-relay-dotnet/branch/master/graph/badge.svg)](https://codecov.io/gh/Azure/azure-relay-dotnet)|
|dev|[![Build status](https://ci.appveyor.com/api/projects/status/qhefoxrdg068xjhj/branch/dev?svg=true)](https://ci.appveyor.com/project/jtaubensee/azure-relay-dotnet/branch/dev) [![codecov](https://codecov.io/gh/Azure/azure-relay-dotnet/branch/dev/graph/badge.svg)](https://codecov.io/gh/Azure/azure-relay-dotnet)|
|Microsoft.Azure.Relay|[![NuGet Version and Downloads count](https://buildstats.info/nuget/Microsoft.Azure.Relay?includePreReleases=true)](https://www.nuget.org/packages/Microsoft.Azure.Relay/)|

This library is built using .NET Standard 2.0. For more information on what platforms are supported see [.NET Platforms Support](https://docs.microsoft.com/en-us/dotnet/articles/standard/library#net-platforms-support).

Azure Relay is one of the key capability pillars of the Azure Service Bus
platform. The Relay’s new "Hybrid Connections" capability is a secure,
open-protocol evolution based on HTTP and WebSockets. It supersedes the former,
equally named "BizTalk Services" feature that was built on a proprietary
protocol foundation. The integration of Hybrid Connections into Azure App
Services will continue to function as-is.

"Hybrid Connections" allows establishing bi-directional, binary stream
communication between two networked applications, whereby either or both parties
can reside behind NATs or Firewalls. This document describes the client-side
interactions with the Hybrid Connections relay for connecting clients in
listener and sender roles and how listeners accept new connections.

This repository contains samples showing how to use the Hybrid Connections
capability from C# and it also holds the protocol documentation.

## How to provide feedback

See our [Contribution Guidelines](./.github/CONTRIBUTING.md).

## Samples

For Relay Hybrid Connections samples, see the [azure/azure-relay](https://github.com/Azure/azure-relay/tree/master/samples/hybrid-connections) service repository.

## Using Hybrid Connections from C# 

The API discussed here is implemented in the new Microsoft.Azure.Relay.dll
assembly, which can be added to your .NET project via a NuGet package.

### Registering Hybrid Connections 

To use the Hybrid Connection feature, you must first register a Hybrid
Connection path with the Relay service. Hybrid Connection paths are string
expressions that uniquely identify the entity. 

To create Hybrid Connection entities, you first need a Service Bus Relay
namespace that you can create either through the Azure portal, the Azure
PowerShell tools, or the cross-platform Azure CLI. 

Existing Azure Relay namespaces can be managed in the Azure Portal, where you
can also add, edit, or remove Hybrid Connection paths interactively. 

The
following two settings are specific to Hybrid Connections: 

| Property                    | Description                          |
|-----------------------------|--------------------------------------|
| RequiresClientAuthorization | If this is set to false (the default is true), sending clients can connect to a listener through the Relay without providing an authorization token. In this case, the Relay will not enforce any if its ownaccess rules, but the listener can still evaluate the Authorization HTTP header or use some other model for access control. |
| ListenerCount               | This is an informational value that’s available via GetRuntimeInformationAsync and gives the number of connected listeners on this Hybrid Connection as the value is queried. |

Up to 25 listeners can be concurrently connected and the Relay will distribute
incoming connection requests across all connected listeners, equivalent to a
network load balancer.

### Handling Tokens

Creating a listener requires an access token that confers the "Listen" right on
the Hybrid Connection entity or at the namespace level. Creating a sender
connection requires, unless the Hybrid Connection entity is configured
otherwise, a token that confers the "Send" right. The follows the [shared access
signature authentication
model](https://azure.microsoft.com/documentation/articles/service-bus-shared-access-signature-authentication/)
that is common across all Service Bus capabilities and entities.

Access tokens are created from an Authorization rule and key using a token
provider helper as described in the article linked above; the Hybrid Connections
API has its own ```TokenProvider``` class, however. The ```TokenProvider``` can
be initialized from a rule and key with
```TokenProvider.CreateSharedAccessSignatureTokenProvider(ruleName, key)``` or
it can be initialized from an existing token string that has been issued by some
other application with ```TokenProvider.CreateSharedAccessSignatureTokenProvider(token)```.

The initialized ```TokenProvider``` instance is used by the ```HybridConnectionListener```
and ```HybridConnectionClient``` API to create tokens as needed. 

However, with Hybrid Connections even more than with other Service Bus features,
you may have scenarios where you will want the Relay to protect your endpoint,
but you also don’t want to hand the SAS rule and key to the client outright. One
such case are browser-based clients. For a browser-based client that needs to
connect to a resource made available via a relayed WebSocket, the server-side
web site can hold on to the required SAS rule and key, and use the ```TokenProvider```
to create a short-lived token string and pass that on to the client: 

```csharp
var token = await TokenProvider.GetTokenAsync("http://namespace.servicebus.windows.net/path", TimeSpan.FromSeconds(30));
var tokenString = token.TokenString;
```

The token created in the exemplary snippet above will only be valid to establish
a connection within 30 seconds of receiving it.

### Creating Listeners 

The Hybrid Connection API follows a very common networking design pattern. There
is a listener object that is first opened to allow incoming connections to flow
and from which the application can then accept these incoming connections for
handling. 

```csharp
var listener = new HybridConnectionListener("sb://namespace.servicebus.windows.net/path", tokenProvider); 
await listener.OpenAsync(TimeSpan.FromSeconds(60)); 
do 
{ 
    var connection = await listener.AcceptConnectionAsync(); 
    Task.Run(()=>this.HandleConnection(connection)); 
} 
while( … ); 
```


The connections are modeled as and based on .NET streams, with
the distinction that they have a ```Shutdown/Async()``` operation that cleanly signals
to the connected party that this process is done sending data, and a
```CloseAsync()``` operation to cleanly close the connection. 

Both of these operations also echo common networking API patterns Since the base
class is ```System.IO.Stream```, the ```HybridConnectionStream``` can be used
with all .NET APIs that expect streams. This includes all standard stream
readers and writers and most common stream data encoders and decoders
(serializers).

The ```HybridConnectionListener``` will
aggressively attempt to stay connected once opened. Should the local network
connection drop or connectivity to the Relay become interrupted, the listener
will patiently retry until the listener can be restored. 

Listeners on clients that are location-agile and may change networks or be put
into sleep mode will also reconnect automatically as circumstances permit. The
application can observe the connection state through the ```Connecting```,
```Online```, and ```Offline``` events that fire when the network status
changes. The ```IsOnline``` property reflects the current connection status, and
```LastError``` provides insight into the reason why the last connection attempt
failed, if the listener transitions its state to ```Connecting``` or ```Offline```.

### Creating Clients 

Client connections are created using the ```HybridConnectionClient``` class.
There are two variants of the constructor: one takes the target address and a
```TokenProvider``` that can produce a "Send" token for the target; the other
omits the token provider for use with Hybrid Connections that are set up without
client authorization.

New connections are created via the ```CreateConnectionAsync()``` method. When
the connection has been established, the method returns a
```HybridConnectionStream``` that is connected to the remote listener. If the
connection attempt fails, a ```RelayException``` will be raised that indicates the
reason for why the connection could not be established.

## How do I run the unit tests? 

In order to run the unit tests, you will need to do the following:

1. Deploy the Azure Resource Manager template located at [/build/azuredeploy.json](./build/azuredeploy.json) by clicking the following button:

    <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-service-bus-dotnet%2Fmaster%2Fbuild%2Fazuredeploy.json" target="_blank">
        <img src="http://azuredeploy.net/deploybutton.png"/>
    </a>

    *Running the above template will provision a namespace along with the required entities to successfully run the unit tests.*

1. Add an Environment Variable named `azure-relay-dotnet/connectionstring` and set the value as the connection string of the newly created namespace. **Please note that if you are using Visual Studio, you must restart Visual Studio in order to use new Environment Variables.**

Once you have completed the above, you can run `dotnet test` from the `/test/Microsoft.Azure.Relay.UnitTests` directory.