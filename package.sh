#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

if [ ${TF_BUILD}="True" ]; then
  BuildConfiguration="Release"
else
  BuildConfiguration="Debug"
fi

echo "dotnet pack '${scriptroot}/Microsoft.Azure.Relay.sln' --no-build --no-dependencies --no-restore /p:Version=${CDP_PACKAGE_VERSION_SEMANTIC:-1.0.0.0-dev} --configuration ${BuildConfiguration}"
dotnet pack "${scriptroot}/Microsoft.Azure.Relay.sln" --no-build --no-dependencies --no-restore /p:Version=${CDP_PACKAGE_VERSION_SEMANTIC:-1.0.0.0-dev} --configuration ${BuildConfiguration}

if [ $? -eq 0 ]
then
  exit 0
else
  echo "Error during running dotnet build" >&2
  exit 1
fi