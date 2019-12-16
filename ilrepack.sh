#!/bin/bash -eu
{ set +x; } 2>/dev/null
SOURCE="${BASH_SOURCE[0]}"
DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"

OS="Mac"
if [[ -e "/c/" ]]; then
  OS="Windows"
fi

REPACK=~/.nuget/packages/avostres.ilrepack/2.1.1/tools/ILRepack.exe
OUTDIR=src/com.unity.rpc/Editor/Rpc/lib
DEPSDIR=deps

pushd $DIR >/dev/null 2>&1

dotnet restore

#$REPACK //internalize /keyfile:common/messagepack.snk /out:$OUTDIR/MessagePack.dll $DEPSDIR/MessagePack.dll $DEPSDIR/System.Threading.Tasks.Extensions.dll
#$REPACK //internalize /keyfile:common/key.snk /out:$OUTDIR/StreamRpc.dll $DEPSDIR/System.Buffers.dll $DEPSDIR/System.IO.Pipelines.dll $DEPSDIR/System.Memory.dll $DEPSDIR/System.Runtime.CompilerServices.Unsafe.dll $DEPSDIR/System.Threading.Tasks.Extensions.dll
#$REPACK /keyfile:common/key.snk /out:$OUTDIR/StreamRpc.dll StreamRpc.dll MessagePack.dll

$REPACK /keyfile:common/key.snk /out:$OUTDIR/StreamRpc.dll $DEPSDIR/StreamRpc.dll $DEPSDIR/MessagePack.dll $DEPSDIR/System.Buffers.dll $DEPSDIR/System.IO.Pipelines.dll $DEPSDIR/System.Memory.dll $DEPSDIR/System.Runtime.CompilerServices.Unsafe.dll $DEPSDIR/System.Threading.Tasks.Extensions.dll

popd >/dev/null 2>&1