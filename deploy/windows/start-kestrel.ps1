# Run as console/Kestrel (useful for smoke test before IIS)
$ErrorActionPreference = "Stop"
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://0.0.0.0:5088"
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force logs, App_Data | Out-Null

$exe = Join-Path $PSScriptRoot "Icewireless.AccountServiceDashboard.Web.exe"
if (Test-Path $exe) {
  & $exe
} else {
  dotnet .\Icewireless.AccountServiceDashboard.Web.dll
}
