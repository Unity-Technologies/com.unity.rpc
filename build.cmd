@echo off
nuget.exe restore
msbuild.exe Unity.Ipc.sln /p:Configuration=Release
