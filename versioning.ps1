# Update values here as well as in PipelineAssemblyInfo.cs, Microsoft.Azure.Relay.csproj (for development builds)
$major = "3"
$minor = "0"
$patch = "1"
$revision = $env:CDP_DEFINITION_BUILD_COUNT_DAY

$buildNumber = "$major.$minor.$patch.$revision"
[Environment]::SetEnvironmentVariable("CustomBuildNumber", $buildNumber, "User")  # This will allow you to use it from env var in later steps of the same phase
Write-Host "##vso[build.updatebuildnumber]${buildNumber}"                         # This will update build number on your build