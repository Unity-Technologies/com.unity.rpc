@echo off
setlocal

SET CONFIGURATION=Debug
SET PUBLIC=""

:loop
IF NOT "%1"=="" (
    IF "%1"=="-p" (
        SET PUBLIC=/p:PublicRelease=true
    )
    IF "%1"=="-r" (
        SET CONFIGURATION=Release
    )
    SHIFT
    GOTO :loop
)

dotnet restore
dotnet build --no-restore -c %CONFIGURATION% %PUBLIC%
