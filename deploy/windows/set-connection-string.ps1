param(
  [Parameter(Mandatory = $true)]
  [string]$ConnectionString
)
$ErrorActionPreference = "Stop"
$path = Join-Path $PSScriptRoot "appsettings.Production.json"
$example = Join-Path $PSScriptRoot "appsettings.Production.json.example"
if (!(Test-Path $path)) {
  if (Test-Path $example) {
    Copy-Item $example $path
  } else {
    throw "Missing $path (and no example template to copy)"
  }
}

# Avoid ConvertTo-Json round-trip (PowerShell can emit invalid JSON / change types).
$raw = [System.IO.File]::ReadAllText($path)
$escaped = $ConnectionString.Replace('\', '\\').Replace('"', '\"')
$pattern = '"IceWirelessOracle"\s*:\s*".*?"'
$replacement = '"IceWirelessOracle": "' + $escaped + '"'
$updated = [regex]::Replace($raw, $pattern, $replacement, 1)
if ($updated -eq $raw) {
  throw "Could not find ConnectionStrings:IceWirelessOracle in $path"
}
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($path, $updated, $utf8)
Write-Host "Updated ConnectionStrings:IceWirelessOracle in appsettings.Production.json"
