#!/bin/sh -eu
{ set +x; } 2>/dev/null
SOURCE="${BASH_SOURCE[0]}"
DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"

pushd $DIR >/dev/null 2>&1
git clean -xdf -e .Editor -e .vs -e .store -e setenv.sh -e upm-ci~/test-results
popd >/dev/null 2>&1