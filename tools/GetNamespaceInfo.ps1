Param
(
    [Parameter(Mandatory=$true, HelpMessage="The ServiceBus namespace. E.g. 'contoso.servicebus.windows.net' or 'contoso'")]
    [string]$Namespace,
    [Parameter(Mandatory=$false)]
    [switch]$NoIPs
)

function Get-SBNamespaceInfo
(
    [string]$ns
)
{
    if (!$ns.Contains("."))
    {
        $ns = $ns + ".servicebus.windows.net"
    }

    $PropertyBag = @{Namespace=$ns}
    $CloudServiceDNS = (Resolve-DnsName $ns -Type CNAME).NameHost
    if ($CloudServiceDNS)
    {
        $CloudServiceVIP = (Resolve-DnsName $CloudServiceDNS -Type A).IPAddress
        $Deployment = $CloudServiceDNS.Split('.')[0].ToUpperInvariant()
        if ($Deployment.StartsWith("NS-SB2-"))
        {
            $Deployment = $Deployment.Substring(7)
        }

        $DirectAddresses = @()
        $instances = 0..127
        $ParentDomain = $ns.Substring($ns.IndexOf('.') + 1)
        $GatewayDnsFormat = ("g{{0}}-{0}-sb.{1}" -f $Deployment.ToLowerInvariant(), $ParentDomain)
        Foreach ($index in $instances)
        {
            $address = ($GatewayDnsFormat -f $index)
            $result = Resolve-DnsName $address -EA SilentlyContinue
            if ($result -ne $null)
            {
                $DirectAddress = ($result | Select-Object Name,IPAddress)
                $DirectAddresses += $DirectAddress
            }
        }

        $PropertyBag = @{Namespace=$ns;CloudServiceDNS=$CloudServiceDNS;Deployment=$Deployment;CloudServiceVIP=$CloudServiceVIP;GatewayDnsFormat=$GatewayDnsFormat;DirectAddresses=$DirectAddresses}
    }

    $details = New-Object PSObject –Property $PropertyBag
    $details
}

$SBDetails = Get-SBNamespaceInfo $Namespace

#Display Summary Info
$SBDetails | Select-Object -Property Namespace,Deployment,CloudServiceDNS,CloudServiceVIP,GatewayDnsFormat | Format-List

if (!$NoIPs.IsPresent)
{
    #Dump the list of Direct IP Addresses
    $SBDetails.DirectAddresses | Format-Table
}