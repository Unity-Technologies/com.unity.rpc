@echo off
setlocal

SET CONFIGURATION=Debug
SET PUBLIC=""
SET BUILD=0

:loop
IF NOT "%1"=="" (
    IF "%1"=="-p" (
        SET PUBLIC=/p:PublicRelease=true
    )
    IF "%1"=="-b" (
        SET BUILD=1
    )
    IF "%1"=="-r" (
        SET CONFIGURATION=Release
    )
    SHIFT
    GOTO :loop
)

if "x%BUILD%"=="x1" (
	dotnet restore
	dotnet build --no-restore -c %CONFIGURATION% %PUBLIC%
)

dotnet pack --no-restore --no-build -c %CONFIGURATION% %PUBLIC%
