<#
.SYNOPSIS
    Mode 2 container verification (ADR 0021): builds the real Release image from the
    Dockerfile, runs it against a throwaway Postgres via compose.verify.yaml, polls
    /health, and reports pass/fail. Always tears the stack down on exit.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot "compose.verify.yaml"
$healthUrl = "http://localhost:8080/health"
$maxAttempts = 30
$delaySeconds = 2

Push-Location $repoRoot
try {
    Write-Host "Building and starting verify stack..."
    docker compose -f $composeFile up -d --build
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed with exit code $LASTEXITCODE"
    }

    $healthy = $false
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 3
            if ($response.status -eq "Healthy") {
                $healthy = $true
                break
            }
            Write-Host "Attempt ${attempt}/${maxAttempts}: status = $($response.status)"
        }
        catch {
            Write-Host "Attempt ${attempt}/${maxAttempts}: not yet reachable"
        }
        Start-Sleep -Seconds $delaySeconds
    }

    if ($healthy) {
        Write-Host "PASS: /health reported Healthy" -ForegroundColor Green
    }
    else {
        Write-Host "FAIL: /health did not report Healthy within $($maxAttempts * $delaySeconds)s" -ForegroundColor Red
        docker compose -f $composeFile logs app
    }
}
finally {
    Write-Host "Tearing down verify stack..."
    docker compose -f $composeFile down
    Pop-Location
}

if (-not $healthy) {
    exit 1
}
exit 0
