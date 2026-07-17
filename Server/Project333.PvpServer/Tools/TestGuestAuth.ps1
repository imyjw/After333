param(
    [string]$BaseUrl = "http://127.0.0.1:7333"
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')

Write-Host "[auth] Verifying that POST /auth/guest is unavailable"
try {
    Invoke-WebRequest `
        -Method Post `
        -Uri "$base/auth/guest" `
        -ContentType "application/json" `
        -Body "{}" `
        -UseBasicParsing | Out-Null
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 404 -or $statusCode -eq 405) {
        Write-Host "[auth] Guest authentication is disabled (HTTP $statusCode)."
        Write-Host "[auth] Guest-auth removal smoke test passed."
        exit 0
    }

    throw
}

throw "POST /auth/guest unexpectedly succeeded. Guest authentication must remain disabled."
