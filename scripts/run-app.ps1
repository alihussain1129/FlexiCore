param([switch]$Clean)
$ErrorActionPreference = "Stop"
$client = "D:\Projects\flexicore\client"
$port   = 5265

if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) { throw "flutter not found in PATH" }
if (-not (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) {
  throw "API is not running on port $port. Run start-dev.ps1 first."
}

# find adb
$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb) {
  foreach ($c in @("$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe", "$env:ANDROID_HOME\platform-tools\adb.exe", "$env:ANDROID_SDK_ROOT\platform-tools\adb.exe")) {
    if ($c -and (Test-Path $c)) { $adb = $c; break }
  }
}
if (-not $adb) { throw "adb.exe not found. Install Android platform-tools or set ANDROID_HOME." }

# pick the physical phone
$devices = (flutter devices --machine | Out-String) | ConvertFrom-Json
$phones = @($devices | Where-Object { $_.targetPlatform -like "android*" -and -not $_.emulator })
if ($phones.Count -eq 0) { throw "No Android phone found. Check USB debugging / wireless debugging." }
$phone = $phones[0]
Write-Host "Using device: $($phone.name) ($($phone.id))" -ForegroundColor Cyan

# port forward (checked properly)
$ErrorActionPreference = "Continue"
$out = & $adb -s $phone.id reverse "tcp:$port" "tcp:$port" 2>&1
if ($LASTEXITCODE -eq 0) { Write-Host "adb reverse OK" -ForegroundColor Green }
else { Write-Host "adb reverse FAILED: $out" -ForegroundColor Red }
$ErrorActionPreference = "Stop"

Set-Location $client

# make Kotlin compile in-process, non-incremental (avoids the corrupted-cache daemon error)
$gp = "$client\android\gradle.properties"
if (Test-Path $gp) {
  $txt = Get-Content $gp -Raw
  foreach ($line in @("kotlin.compiler.execution.strategy=in-process", "kotlin.incremental=false")) {
    $key = ($line -split '=')[0]
    if ($txt -notmatch "(?m)^\s*$([regex]::Escape($key))\s*=") { Add-Content $gp "`n$line"; Write-Host "Added $line to gradle.properties" -ForegroundColor Gray }
  }
}

# stop stale daemons and wipe build caches
if ($Clean) {
  Write-Host "Cleaning build caches..." -ForegroundColor Cyan
  $ErrorActionPreference = "Continue"
  if (Test-Path "$client\android\gradlew.bat") { & "$client\android\gradlew.bat" --stop 2>&1 | Out-Null }
  Start-Sleep -Seconds 2
  foreach ($d in @("$client\build", "$client\.dart_tool\flutter_build", "$client\android\.gradle")) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue }
  }
  $ErrorActionPreference = "Stop"
}

flutter pub get
if ($LASTEXITCODE -ne 0) { throw "flutter pub get failed" }

Write-Host "`nStarting the normal FlexiCore ERP app (not the pairing-only app)." -ForegroundColor Green
Write-Host "Keys: r = hot reload, R = restart, q = quit`n" -ForegroundColor Gray
flutter run -t lib/main.dart -d "$($phone.id)"

