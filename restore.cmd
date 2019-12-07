@ECHO OFF
PUSHD "%~dp0"

echo dotnet restore "%~dp0Microsoft.Azure.Relay.sln"
call dotnet restore "%~dp0Microsoft.Azure.Relay.sln" || (
    echo ERROR: couldn't restore packages
    exit /B 1
)

POPD
EXIT /B 0