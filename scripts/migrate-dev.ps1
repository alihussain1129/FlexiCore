$ErrorActionPreference = "Stop"
$root      = "D:\Projects\flexicore"
$apiProj   = "$root\backend\src\FlexiCore.Api\FlexiCore.Api.csproj"
$migProj   = (Get-ChildItem "$root\backend\src" -Directory -Filter "FlexiCore.Migrations" | Select-Object -First 1).FullName
$container = "flexicore-postgres-1"

# database name from user secrets
$db = $null
foreach ($line in @(dotnet user-secrets list --project $apiProj 2>&1)) {
  if ("$line" -match '^\s*ConnectionStrings:Default\s*=\s*(.+)$') {
    foreach ($kv in ($Matches[1] -split ';')) { if ($kv -match '^\s*database\s*=\s*(.*)$') { $db = $Matches[1].Trim() } }
  }
}
if (-not $db) { throw "Database name not found in user secrets" }

# find .env: compose working dir label, then search the repo
$candidates = @()
$info = docker inspect $container | ConvertFrom-Json
$wd = $info[0].Config.Labels.'com.docker.compose.project.working_dir'
if ($wd) { $candidates += (Join-Path $wd ".env") }
$candidates += "$root\.env", "$root\backend\.env", "$root\deploy\.env"
$candidates += @(Get-ChildItem $root -Recurse -Force -Filter ".env*" -File -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch '\\(node_modules|\.git|\.dart_tool|build|bin|obj)\\' } |
  ForEach-Object FullName)

$ownerPw = $null
foreach ($f in ($candidates | Select-Object -Unique)) {
  if (Test-Path $f) {
    foreach ($l in Get-Content $f) {
      if ($l -match '^\s*FLEXICORE_OWNER_PASSWORD\s*=\s*(.*)$') { $ownerPw = $Matches[1].Trim().Trim('"').Trim("'") }
    }
    if ($ownerPw) { Write-Host "Password read from $f"; break }
  }
}
if (-not $ownerPw) {
  $sec = Read-Host "flexicore_owner password" -AsSecureString
  $ownerPw = [System.Net.NetworkCredential]::new("", $sec).Password
}
if (-not $ownerPw) { throw "No owner password available" }

$hostPort = ((docker port $container 5432 | Select-Object -First 1) -split ':')[-1].Trim()

$env:PGPASSWORD = $ownerPw
try {
  Write-Host "--- Running migrations ---" -ForegroundColor Cyan
  $env:FLEXICORE_MIGRATIONS_CONNECTION = "Host=localhost;Port=$hostPort;Database=$db;Username=flexicore_owner;Password=$ownerPw"
  dotnet run --project $migProj
  if ($LASTEXITCODE -ne 0) { throw "Migration runner failed. Backup: D:\Backups\flexicore\flexicore-20261019-110940.sql" }

  Write-Host "--- Applied after ---" -ForegroundColor Cyan
  docker exec -e PGPASSWORD $container psql -h 127.0.0.1 -U flexicore_owner -d $db -At -c "select version from public.schema_migrations order by 1"
  Write-Host "--- Device tables ---" -ForegroundColor Cyan
  docker exec -e PGPASSWORD $container psql -h 127.0.0.1 -U flexicore_owner -d $db -At -c "select table_schema||'.'||table_name from information_schema.tables where table_schema='devices' order by 1"
  Write-Host "Done. Retry the pairing-code command." -ForegroundColor Green
}
finally {
  Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
  Remove-Item Env:FLEXICORE_MIGRATIONS_CONNECTION -ErrorAction SilentlyContinue
}
