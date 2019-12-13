#!/bin/bash -eu
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
UNITYVERSION=2019.2
YAMATO=0

while (( "$#" )); do
  case "$1" in
    -d|--debug)
      CONFIGURATION="Debug"
    ;;
    -r|--release)
      CONFIGURATION="Release"
    ;;
    -p|--public)
      PUBLIC="-p:PublicRelease=true"
    ;;
    -b|--build)
      BUILD=1
    ;;
    -u|--upm)
      UPM=1
    ;;
    -c)
      shift
      CONFIGURATION=$1
    ;;
    -g|--github)
      GITHUB=1
    ;;
    -*|--*=) # unsupported flags
      echo "Error: Unsupported flag $1" >&2
      exit 1
    ;;
  esac
  shift
done

if [[ x"${YAMATO_JOB_ID:-}" != x"" ]]; then
  YAMATO=1
  export GITLAB_CI=1
  export CI_COMMIT_TAG="${GIT_TAG:-}"
  export CI_COMMIT_REF_NAME="${GIT_BRANCH:-}"
fi

if [[ x"${PUBLISH_KEY:-}" == x"" ]]; then
  echo "Can't publish without a PUBLISH_KEY environment variable in the user:token format" >&2
  popd >/dev/null 2>&1
  exit 1
fi

if [[ x"${PUBLISH_URL:-}" == x"" ]]; then
  echo "Can't publish without a PUBLISH_URL environment variable" >&2
  popd >/dev/null 2>&1
  exit 1
fi

for p in "$DIR/build/nuget/**/*nupkg"; do
  dotnet nuget push $p -ApiKey "${PUBLISH_KEY}" -Source "${PUBLISH_URL}"
done
