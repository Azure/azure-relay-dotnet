@ECHO OFF
PUSHD "%~dp0"

if defined TF_BUILD (
    set BuildConfiguration=Release
    set BLAMEFLAG=--blame
) else (
    set BuildConfiguration=Debug
    set BLAMEFLAG=
)

echo dotnet test "%~dp0Microsoft.Azure.Relay.sln" --no-build --no-restore %BLAMEFLAG% --logger:trx --configuration %BuildConfiguration%
call dotnet test "%~dp0Microsoft.Azure.Relay.sln" --no-build --no-restore %BLAMEFLAG% --logger:trx --configuration %BuildConfiguration% || (
    echo Failed to run tests
    exit /b 1
)

REM ------------------------------------------------------------


REM ------------------------------------------------------------

POPD
EXIT /B 0