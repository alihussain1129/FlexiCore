param([switch]$Owner)
$ErrorActionPreference = "Stop"
$root    = "D:\Projects\flexicore"
$apiProj = "$root\backend\src\FlexiCore.Api\FlexiCore.Api.csproj"
$launch  = "$root\backend\src\FlexiCore.Api\Properties\launchSettings.json"
$env:Deployment__Mode = "Local"
$env:License__Directory = "$root\.local-licenses"

$raw = Get-Content $launch -Raw
$m = [regex]::Match($raw, '"applicationUrl"\s*:\s*"([^"]+)"')
if (-not $m.Success) { throw "applicationUrl not found in launchSettings.json" }
$url  = ($m.Groups[1].Value -split ';')[0]
$port = ([uri]$url).Port

# 1. Start the API in its own window (that window shows the API log; closing it stops the API)
$already = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
if ($already) {
  try { $licenseStatus = Invoke-RestMethod -Uri "$url/api/v1/license/status" -TimeoutSec 5 }
  catch { throw "Port $port is occupied, but its listener did not pass the local license check. Identify it before continuing." }
  if ($licenseStatus.mode -ne "local") { throw "Port $port is serving mode '$($licenseStatus.mode)', not the required Local mode." }
  Write-Host "Local API already running on $url" -ForegroundColor Green
} else {
  Start-Process powershell -ArgumentList "-NoExit","-Command","dotnet run --project `"$apiProj`""
  Write-Host "API starting in a new window..." -ForegroundColor Cyan
}

# 2. Wait until it answers (up to 3 minutes)
$up = $false
for ($i = 0; $i -lt 90; $i++) {
  try {
    $health = Invoke-WebRequest -UseBasicParsing -Uri "$url/health/ready" -TimeoutSec 2
    $licenseStatus = Invoke-RestMethod -Uri "$url/api/v1/license/status" -TimeoutSec 2
    if ($health.StatusCode -eq 200 -and $licenseStatus.mode -eq "local") { $up = $true; break }
  } catch { Start-Sleep -Seconds 2 }
}
if (-not $up) { throw "API did not start within 3 minutes. Check the API window for the error." }
Write-Host "API is up on $url" -ForegroundColor Green

# 3. USB forward to the phone
$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb) {
  foreach ($c in @("$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe", "$env:ANDROID_HOME\platform-tools\adb.exe", "$env:ANDROID_SDK_ROOT\platform-tools\adb.exe")) {
    if ($c -and (Test-Path $c)) { $adb = $c; break }
  }
}
if ($adb) {
  $ErrorActionPreference = "Continue"
  $serials = @(& $adb devices 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -match "`tdevice$" } | ForEach-Object { ($_ -split "`t")[0] })
  $targets = @($serials | Where-Object { $_ -notmatch '^emulator-' -and $_ -notmatch '_adb-tls-connect' })
  if ($targets.Count -eq 0) { $targets = $serials }
  foreach ($s in $targets) {
    $out = & $adb -s $s reverse "tcp:$port" "tcp:$port" 2>&1
    if ($LASTEXITCODE -eq 0) { Write-Host "adb reverse OK on $s" -ForegroundColor Green }
    else { Write-Host "adb reverse FAILED on ${s}: $out" -ForegroundColor Red }
  }
  if ($targets.Count -eq 0) { Write-Host "No phone connected (check USB debugging)" -ForegroundColor Yellow }
  $ErrorActionPreference = "Stop"
}

# 4. Pairing code, created last so it is fresh
$cs = $null
foreach ($line in @(dotnet user-secrets list --project $apiProj 2>&1)) {
  if ("$line" -match '^\s*ConnectionStrings:Default\s*=\s*(.+)$') { $cs = $Matches[1].Trim() }
}
if (-not $cs) { throw "User secret ConnectionStrings:Default not found" }
$env:ConnectionStrings__Default = $cs
try {
  $cmdArgs = @("pairing-code")
  if ($Owner) { $cmdArgs += "--owner" }
  Write-Host "`n=== PAIRING CODE - enter it in the app now ===" -ForegroundColor Cyan
  dotnet run --project $apiProj --no-launch-profile -- @cmdArgs
  if ($LASTEXITCODE -ne 0) { throw "pairing-code failed (exit $LASTEXITCODE)" }
}
finally { Remove-Item Env:ConnectionStrings__Default -ErrorAction SilentlyContinue }

Write-Host "`nApp API address: http://localhost:$port" -ForegroundColor Green
Write-Host "To stop the API, close its window or press Ctrl+C in it." -ForegroundColor Gray
