@echo off
cd /d "%~dp0"
dotnet run --project "src\VDisplay.Helper\VDisplay.Helper.csproj" -c Release -p:PlatformTarget=x64
