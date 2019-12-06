@ECHO OFF
PUSHD "%~dp0"

rem CDP_PACKAGE_VERSION_SEMANTIC set by internal 
if not defined CDP_PACKAGE_VERSION_SEMANTIC set CDP_PACKAGE_VERSION_SEMANTIC=2.0.1.0-dev

if defined TF_BUILD (
    set BuildConfiguration=Release
) else (
    set BuildConfiguration=Debug
)

echo dotnet pack "%~dp0Microsoft.Azure.Relay.sln" --no-build --no-dependencies --no-restore /p:Version=%CDP_PACKAGE_VERSION_SEMANTIC% --configuration %BuildConfiguration%
call dotnet pack "%~dp0Microsoft.Azure.Relay.sln" --no-build --no-dependencies --no-restore /p:Version=%CDP_PACKAGE_VERSION_SEMANTIC% --configuration %BuildConfiguration% || (
   echo ERROR: failed to create nuget packages
   exit /B 1
)

REM ------------------------------------------------------------

POPD
EXIT /B 0