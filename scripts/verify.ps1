<#
.SYNOPSIS
    Mode 2 container verification (ADR 0021): builds the real Release image from the
    Dockerfile, runs it against a throwaway Postgres via compose.verify.yaml, asserts
    /health, non-root uid, and SPA serving, and reports pass/fail. Always tears the
    stack down on exit.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot "compose.verify.yaml"
$healthUrl = "http://localhost:8080/health"
$rootUrl = "http://localhost:8080/"
$maxAttempts = 30
$delaySeconds = 2
$passed = $false

Push-Location $repoRoot
try {
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

        if (-not $healthy) {
            Write-Host "FAIL: /health did not report Healthy within $($maxAttempts * $delaySeconds)s" -ForegroundColor Red
            docker compose -f $composeFile logs app
            return
        }
        Write-Host "/health reported Healthy (migrate-on-startup applied the schema; app reached Postgres over the Docker network)"

        $uid = (docker compose -f $composeFile exec -T app id -u).Trim()
        if ($uid -ne "1000") {
            Write-Host "FAIL: container runs as uid '$uid', expected 1000" -ForegroundColor Red
            return
        }
        Write-Host "Container runs as non-root uid 1000"

        $indexHtml = Invoke-RestMethod -Uri $rootUrl -Method Get -TimeoutSec 3
        if ($indexHtml -notmatch "<html") {
            Write-Host "FAIL: GET / did not return the built SPA's index.html" -ForegroundColor Red
            return
        }
        Write-Host "Built SPA is served from the container"

        $passed = $true
        Write-Host "PASS: image builds, reaches Postgres over the network, migrates, runs as uid 1000, and serves the SPA" -ForegroundColor Green
    }
    catch {
        Write-Host "FAIL: $_" -ForegroundColor Red
        docker compose -f $composeFile logs app
    }
}
finally {
    Write-Host "Tearing down verify stack..."
    docker compose -f $composeFile down
    Pop-Location
}

if (-not $passed) {
    exit 1
}
exit 0
