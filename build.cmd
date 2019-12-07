@ECHO OFF
PUSHD "%~dp0"

if defined TF_BUILD (
    set BuildConfiguration=Release
) else (
    set BuildConfiguration=Debug
)

echo dotnet build "%~dp0Microsoft.Azure.Relay.sln" --configuration %BuildConfiguration%
call dotnet build "%~dp0Microsoft.Azure.Relay.sln" --configuration %BuildConfiguration% || (
    echo ERROR: Failed to build main platforms
    exit /B 1
)

POPD
EXIT /B 0