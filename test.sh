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

echo "dotnet test '${scriptroot}/Microsoft.Azure.Relay.sln' --no-build --no-restore --blame --logger:trx --configuration ${BuildConfiguration}"
dotnet test "${scriptroot}/Microsoft.Azure.Relay.sln" --no-build --no-restore --blame --logger:trx --configuration ${BuildConfiguration}

if [ $? -eq 0 ]
then
  exit 0
else
  echo "Error during running dotnet test" >&2
  exit 1
fi