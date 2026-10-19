param([switch]$Owner)
$ErrorActionPreference = "Stop"
$apiProj = "D:\Projects\flexicore\backend\src\FlexiCore.Api\FlexiCore.Api.csproj"

$cs = $null
foreach ($line in @(dotnet user-secrets list --project $apiProj 2>&1)) {
    if ("$line" -match '^\s*ConnectionStrings:Default\s*=\s*(.+)$') { $cs = $Matches[1].Trim() }
}
if (-not $cs) { throw "User secret ConnectionStrings:Default not found" }

$env:ConnectionStrings__Default = $cs
try {
    $cmdArgs = @("pairing-code")
    if ($Owner) { $cmdArgs += "--owner" }
    dotnet run --project $apiProj --no-build --no-launch-profile -- @cmdArgs
    if ($LASTEXITCODE -ne 0) { throw "pairing-code failed (exit $LASTEXITCODE)" }
}
finally {
    Remove-Item Env:ConnectionStrings__Default -ErrorAction SilentlyContinue
}
