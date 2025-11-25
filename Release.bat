@echo off
REM Build the .NET project in Release configuration
call dotnet build --configuration Release

REM Wait for a key press before closing the window
pause