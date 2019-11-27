#!/bin/sh -eu
{ set +x; } 2>/dev/null
SOURCE="${BASH_SOURCE[0]}"
DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"

OS="Mac"
if [[ -e "/c/" ]]; then
  OS="Windows"
fi

CONFIGURATION=Release
PUBLIC=""
BUILD=0
UPM=0

while (( "$#" )); do
  case "$1" in
    -d|--debug)
      CONFIGURATION="Debug"
      shift
    ;;
    -r|--release)
      CONFIGURATION="Release"
      shift
    ;;
    -p|--public)
      PUBLIC="/p:PublicRelease=true"
      shift
    ;;
    -b|--build)
      BUILD=1
      shift
    ;;
    -u|--upm)
      UPM=1
      shift
    ;;
    -*|--*=) # unsupported flags
      echo "Error: Unsupported flag $1" >&2
      exit 1
      ;;
  esac
done

if [[ x"$OS" == x"Windows" && x"$PUBLIC" != x"" ]]; then
  PUBLIC="/$PUBLIC"
fi

pushd $DIR >/dev/null 2>&1
if [[ x"$BUILD" == x"1" ]]; then
  dotnet restore
  dotnet build --no-restore -c $CONFIGURATION $PUBLIC
fi
dotnet pack --no-build --no-restore -c $CONFIGURATION $PUBLIC

if [[ x"$UPM" == x"1" ]]; then
  powershell scripts/Pack-Upm.ps1
fi
popd >/dev/null 2>&1
