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
    $future = "FUTURE"
    if (!$ns.Contains("."))
    {
        $ns = $ns + ".servicebus.windows.net"
    }

    $PropertyBag = @{Namespace=$ns}
    $CloudServiceDNS = (Resolve-DnsName $ns -Type CNAME).NameHost

    if ($CloudServiceDNS.contains(".privatelink.servicebus"))
    {
        $CloudServiceDNS = (Resolve-DnsName $CloudServiceDNS -Type CNAME).NameHost
    }
    
    if ($CloudServiceDNS)
    {
        $CloudServiceVIP = (Resolve-DnsName $CloudServiceDNS -Type A).IPAddress
        $Deployment = $CloudServiceDNS.Split('.')[0].ToUpperInvariant()
        if ($Deployment.StartsWith("NS-SB2-"))
        {
            $Deployment = $Deployment.Substring(7)
        }
	    if ($Deployment.StartsWith("NS-"))
        {
            $Deployment = $Deployment.Substring(3)
        }
        if (!($Deployment -like "*-v*")){
            $checkForNewNodes = $True
        }

        $DirectAddresses = @()
        $GvDirectAddresses = @()
        $instances = 0..127
        $ParentDomain = $ns.Substring($ns.IndexOf('.') + 1)
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
        if($checkForNewNodes)
        {
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
        }
        
        $Disclaimer = "Entries with 'FUTURE' IPAddress may be added at a later time as needed"
        
        $PropertyBag = @{Namespace=$ns;CloudServiceDNS=$CloudServiceDNS;Deployment=$Deployment;CloudServiceVIP=$CloudServiceVIP;GatewayDnsFormat=$oldGatewayDnsFormat;NewGatewayDnsFormat=$GatewayDnsFormat;DirectAddresses=$DirectAddresses;GvDirectAddresses=$GvDirectAddresses;Notes=$Disclaimer;CheckForNewNodes=$checkForNewNodes}
    }

    $details = New-Object PSObject -Property $PropertyBag
    $details
}

$SBDetails = Get-SBNamespaceInfo $Namespace

$checkForNewNodes = $SBDetails | Select-Object -Property CheckForNewNodes

#Display Summary Info
if($checkForNewNodes.checkForNewNodes)
{
    $SBDetails | Select-Object -Property Namespace,Deployment,CloudServiceDNS,CloudServiceVIP,GatewayDnsFormat,NewGatewayDnsFormat,Notes | Format-List
} else
{
    $SBDetails | Select-Object -Property Namespace,Deployment,CloudServiceDNS,CloudServiceVIP,GatewayDnsFormat,Notes | Format-List
}

$newNodesWarning = $SBDetails | Select-Object -Property newNodesAdded

if($checkForNewNodes.checkForNewNodes){
    #Update 
    Write-Host "ATTENTION: New Gateway DNS Format Starting With 'gv...' Detected" -ForegroundColor Yellow
}

if (!$NoIPs.IsPresent)
{
    #Dump the list of Direct IP Addresses
    $SBDetails.DirectAddresses | Format-Table

    if($checkForNewNodes.checkForNewNodes){
    #Update 
    Write-Host "ATTENTION: New Gateway DNS Format Starting With 'gv...' Detected" -ForegroundColor Yellow
    $SBDetails.GvDirectAddresses | Format-Table
    }
    
}
