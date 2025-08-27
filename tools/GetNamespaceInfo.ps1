Param
(
    [Parameter(Mandatory=$true, HelpMessage="The ServiceBus namespace. E.g. 'contoso.servicebus.windows.net' or 'contoso'")]
    [string]$Namespace,
    [Parameter(Mandatory=$false)]
    [switch]$NoIPs
)
    # Get the directory where the script is located
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    
    # Load the JSON file
    $jsonFilePath = Join-Path -Path $scriptPath -ChildPath "RelayRegionalIPRanges.json"

    function Get-FutureIPRangesByRegion {
    param (
        [Parameter(Mandatory=$true)]
        [string]$RelayServiceTagIPFilePath,
        [Parameter(Mandatory=$true)]
        [string]$Region
    )
    
    $relayServiceTagIPPrefixes = Get-Content -Path $RelayServiceTagIPFilePath -Raw | ConvertFrom-Json

    # Filter objects by the specified region
    $regionEntries = $relayServiceTagIPPrefixes | Where-Object { $_.name -eq "Relay.$Region" }

    if ($regionEntries -eq $null) {
        # List available regions for reference
        $availableRegions = $relayServiceTagIPPrefixes | Select-Object -ExpandProperty properties | Select-Object -ExpandProperty region -Unique | Sort-Object
    } else {
        return $regionEntries.properties.addressPrefixes | ForEach-Object {
            [PSCustomObject]@{
                addressPrefixes = $_
            }
        }
    }
}

function Get-SBNamespaceInfo
(
    [string]$ns
)
{
    $checkForNewNodes = $True
    $future = "FUTURE"
    if (!$ns.Contains("."))
    {
        $ns = $ns + ".servicebus.windows.net"
    }

    $PropertyBag = @{Namespace=$ns}
    $clusterDNS = (Resolve-DnsName $ns -Type CNAME).NameHost

    if ($clusterDNS.contains(".privatelink.servicebus"))
    {
        $clusterDNS = (Resolve-DnsName $clusterDNS -Type CNAME).NameHost
    }

    if ($clusterDNS)
    {
        $ClusterVIP = (Resolve-DnsName $clusterDNS -Type A).IPAddress
        $Deployment = $clusterDNS.Split('.')[0].ToUpperInvariant()
	    if ($Deployment.StartsWith("NS-"))
        {
            $Deployment = $Deployment.Substring(3)
        }
        if (!($Deployment -like "*-v*")){
            $checkForNewNodes = $True
        }

        $DirectAddresses = @()
        $GvDirectAddresses = @()
        $instances = 0..99
        $ParentDomain = $ns.Substring($ns.IndexOf('.') + 1)
        $region = $clusterDNS.Split('.')[1] # Extract the region from the cluster DNS name  
        $GatewayDnsFormat = ("g{{0}}-{0}-sb.{1}" -f $Deployment.ToLowerInvariant(), $ParentDomain)
        $newNodesAdded = $false
        Foreach ($index in $instances)
        {
            $address = ($GatewayDnsFormat -f $index)
            $result = Resolve-DnsName $address -EA SilentlyContinue
            if ($result -ne $null)
            {
                $DirectAddress = ($result | Select-Object Name,IPAddress)
                $DirectAddresses += $DirectAddress
            }
            else
            {
                $temp = New-Object -TypeName PSObject
                Add-Member -InputObject $temp -MemberType NoteProperty -Name Name -Value $address
                Add-Member -InputObject $temp -MemberType NoteProperty -Name IPAddress -Value $future
                $DirectAddress = $temp
                $DirectAddresses += $DirectAddress
            }
        }
        $oldGatewayDnsFormat = $GatewayDnsFormat
	    $GatewayDnsFormat = ("gv{{0}}-{0}-sb.{1}" -f $Deployment.ToLowerInvariant(), $ParentDomain)
        Foreach ($index in $instances)
        {
        $address = ($GatewayDnsFormat -f $index)
        $result = Resolve-DnsName $address -EA SilentlyContinue
            if ($result -ne $null)
            {
                $GvDirectAddress = ($result | Select-Object Name,IPAddress)
                $GvDirectAddresses += $GvDirectAddress
            }
            else
            {
                $temp = New-Object -TypeName PSObject
                Add-Member -InputObject $temp -MemberType NoteProperty -Name Name -Value $address
                Add-Member -InputObject $temp -MemberType NoteProperty -Name IPAddress -Value $future
                $GvDirectAddress = $temp
                $GvDirectAddresses += $GvDirectAddress
            }
        }
        
        $FutureIPRangesForRegion = Get-FutureIPRangesByRegion -RelayServiceTagIPFilePath $jsonFilePath -Region $region

        $Disclaimer = "Entries with 'FUTURE' IPAddress may be added at a later time as needed"

        $PropertyBag = @{Namespace=$ns;ClusterDNS=$ClusterDNS;Deployment=$Deployment;ClusterVIP=$ClusterVIP;GatewayDnsFormat=$oldGatewayDnsFormat;AlternateGatewayDnsFormat=$GatewayDnsFormat;DirectAddresses=$DirectAddresses;GvDirectAddresses=$GvDirectAddresses;Notes=$Disclaimer;CheckForNewNodes=$checkForNewNodes;ClusterRegion=$region;FutureIPRanges=$FutureIPRangesForRegion}
    }

    $details = New-Object PSObject -Property $PropertyBag
    $details
}

$SBDetails = Get-SBNamespaceInfo $Namespace

$checkForNewNodes = $SBDetails | Select-Object -Property CheckForNewNodes

#Display Summary Info
$SBDetails | Select-Object -Property Namespace,Deployment,ClusterDNS,ClusterRegion,ClusterVIP,GatewayDnsFormat,AlternateGatewayDnsFormat,Notes | Format-List

$newNodesWarning = $SBDetails | Select-Object -Property newNodesAdded

if($checkForNewNodes.checkForNewNodes){
    #Update 
    Write-Host "Alternate Gateway DNS Format Starting With 'gv...' Detected" -ForegroundColor Yellow
}

if (!$NoIPs.IsPresent)
{
    #Dump the list of Direct IP Addresses
    $SBDetails.DirectAddresses | Format-Table

    if($checkForNewNodes.checkForNewNodes)
    {
        #Update 
        Write-Host "Alternate Gateway DNS Format Starting With 'gv...' Detected" -ForegroundColor Yellow
        $SBDetails.GvDirectAddresses | Format-Table
    } 
}

if ($SBDetails.FutureIPRanges.IsPresent)
{
    Write-Host "Future IP Ranges for Region:$($SBDetails.ClusterRegion)" -ForegroundColor Cyan
    $SBDetails.FutureIPRanges | Format-Table -Property addressPrefixes
}
