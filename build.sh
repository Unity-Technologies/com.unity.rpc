#!/bin/bash
nuget restore 
msbuild Unity.Ipc.sln /p:Configuration=Release

