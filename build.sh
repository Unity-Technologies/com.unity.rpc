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
dotnet build-server shutdown >/dev/null 2>&1 || true
dotnet restore
dotnet build --no-restore -c $CONFIGURATION $PUBLIC
popd >/dev/null 2>&1