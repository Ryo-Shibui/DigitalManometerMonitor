$ErrorActionPreference = "Stop"
dotnet build ".\DigitalManometerMonitor.sln" -c Release -p:Platform=x64
Write-Host "Release build completed."
