@echo off
setlocal

SET CONFIGURATION=Release
SET PUBLIC=""
SET BUILD=0
set UPM=0

:loop
IF NOT "%1"=="" (
    IF "%1"=="-p" (
        SET PUBLIC=/p:PublicRelease=true
    )
    IF "%1"=="--public" (
        SET PUBLIC=/p:PublicRelease=true
    )
    IF "%1"=="-b" (
        SET BUILD=1
    )
    IF "%1"=="--build" (
        SET BUILD=1
    )
    IF "%1"=="-d" (
        SET CONFIGURATION=Debug
    )
    IF "%1"=="--debug" (
        SET CONFIGURATION=Debug
    )
    IF "%1"=="-r" (
        SET CONFIGURATION=Release
    )
    IF "%1"=="--release" (
        SET CONFIGURATION=Release
    )
    IF "%1"=="-u" (
        SET UPM=1
    )
    IF "%1"=="--upm" (
        SET UPM=1
    )
    SHIFT
    GOTO :loop
)

if "x%BUILD%"=="x1" (
	dotnet restore
	dotnet build --no-restore -c %CONFIGURATION% %PUBLIC%
)
dotnet test --no-build --no-restore -c %CONFIGURATION% %PUBLIC% --logger "trx;LogFileName=dotnet-test-result.trx"

if "x%UPM%"=="x1" (
  call powershell scripts/Test-Upm.ps1
)
