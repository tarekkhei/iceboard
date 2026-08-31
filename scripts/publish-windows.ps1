# Publish self-contained win-x64 package (avoids needing shared .NET 10 runtime on server).
# IIS still needs the .NET 10 Hosting Bundle for AspNetCoreModuleV2.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$props = Join-Path $root "Directory.Build.props"
$version = ([regex]::Match((Get-Content $props -Raw), '<Version>([^<]+)</Version>')).Groups[1].Value
$proj = Join-Path $root "src/Icewireless.AccountServiceDashboard.Web/Icewireless.AccountServiceDashboard.Web.csproj"
$out = Join-Path $root "artifacts/publish/win-x64"
$zipDir = Join-Path $root "artifacts"
$zip = Join-Path $zipDir "Icewireless.AccountServiceDashboard-win-x64-$version.zip"
$zipAlias = Join-Path $zipDir "Icewireless.AccountServiceDashboard-win-x64.zip"
$deploy = Join-Path $root "deploy/windows"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out, $zipDir | Out-Null

dotnet publish $proj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o $out `
  /p:PublishSingleFile=false

Copy-Item (Join-Path $deploy "DEPLOY-WINDOWS.md") (Join-Path $out "DEPLOY-WINDOWS.md") -Force
Copy-Item (Join-Path $root "RELEASE-NOTES.md") (Join-Path $out "RELEASE-NOTES.md") -Force
Copy-Item (Join-Path $deploy "start-kestrel.ps1") (Join-Path $out "start-kestrel.ps1") -Force
Copy-Item (Join-Path $deploy "set-connection-string.ps1") (Join-Path $out "set-connection-string.ps1") -Force
Copy-Item (Join-Path $deploy "web.config") (Join-Path $out "web.config") -Force
$prodJson = Join-Path $out "appsettings.Production.json"
if (Test-Path $prodJson) { Remove-Item $prodJson -Force }
Copy-Item (Join-Path $deploy "appsettings.Production.json") (Join-Path $out "appsettings.Production.json.example") -Force
$guide = Join-Path $root "artifacts/user-guide/Dashboard-User-Guide.html"
if (Test-Path $guide) {
  Copy-Item $guide (Join-Path $out "Dashboard-User-Guide.html") -Force
}
Set-Content -Path (Join-Path $out "VERSION.txt") -Value $version -NoNewline

if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $zipAlias) { Remove-Item $zipAlias -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -Force
Copy-Item $zip $zipAlias -Force
Copy-Item (Join-Path $out "DEPLOY-WINDOWS.md") (Join-Path $zipDir "DEPLOY-WINDOWS.md") -Force
Copy-Item (Join-Path $root "RELEASE-NOTES.md") (Join-Path $zipDir "RELEASE-NOTES.md") -Force

Write-Host "Version: $version"
Write-Host "Published: $out"
Write-Host "Zip: $zip"
