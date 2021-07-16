# Generate a custom version
$major = "3"
$minor = "0"
$patch = "0"
$revision = $env:CDP_DEFINITION_BUILD_COUNT     # Total number of builds ever queued from this definition

$buildNumber = "$major.$minor.$patch.$revision"
[Environment]::SetEnvironmentVariable("CustomBuildNumber", $buildNumber, "User")  # This will allow you to use it from env var in later steps of the same phase
Write-Host "##vso[build.updatebuildnumber]${buildNumber}"                         # This will update build number on your build
