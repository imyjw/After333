param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$ClientVersion = "0.1.0-dev",
    [string]$CardDatabasePath = "C:\Project_333\Assets\Project333\Resources\Project333\Data\cards.json",
    [string]$GameId = "",
    [string]$Password = "dev_password_333"
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost {
    param(
        [string]$Path,
        [object]$Body,
        [hashtable]$Headers = @{}
    )

    return Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl$Path" `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json -Compress -Depth 8)
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Rejected {
    param(
        [string]$Path,
        [object]$Body,
        [hashtable]$Headers,
        [int]$ExpectedStatusCode,
        [string]$ExpectedErrorCode
    )

    try {
        Invoke-JsonPost -Path $Path -Body $Body -Headers $Headers | Out-Null
        throw "Expected $Path to be rejected with HTTP $ExpectedStatusCode."
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) {
            throw
        }

        $actualStatusCode = [int]$response.StatusCode
        Assert-True `
            ($actualStatusCode -eq $ExpectedStatusCode) `
            "$Path returned HTTP $actualStatusCode, expected $ExpectedStatusCode."

        $body = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($body)) {
            try {
                $stream = $response.GetResponseStream()
                if ($null -ne $stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    try {
                        $body = $reader.ReadToEnd()
                    }
                    finally {
                        $reader.Dispose()
                    }
                }
            }
            catch {
                $body = ""
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedErrorCode)) {
            Assert-True `
                ($body -match [regex]::Escape($ExpectedErrorCode)) `
                "$Path error did not contain '$ExpectedErrorCode'. Body: $body"
        }
    }
}

if (-not (Test-Path -LiteralPath $CardDatabasePath -PathType Leaf)) {
    throw "Card database was not found: $CardDatabasePath"
}

$cardDatabase = Get-Content -LiteralPath $CardDatabasePath -Raw | ConvertFrom-Json
$rarityByCardId = @{}
foreach ($card in $cardDatabase.cards) {
    $rarityByCardId[$card.id] = $card.rarity
}

if ([string]::IsNullOrWhiteSpace($GameId)) {
    $GameId = "draft.$([guid]::NewGuid().ToString('N').Substring(0, 10))"
}

Write-Host "[draft] POST /auth/register gameId=$GameId"
$auth = Invoke-JsonPost -Path "/auth/register" -Body @{
    gameId = $GameId
    password = $Password
    displayName = $GameId
    clientVersion = $ClientVersion
}
Assert-True (-not [string]::IsNullOrWhiteSpace($auth.sessionToken)) "Game ID registration did not return a session token."

$headers = @{
    Authorization = "Bearer $($auth.sessionToken)"
}

Write-Host "[draft] POST /runs/start"
$started = Invoke-JsonPost -Path "/runs/start" -Headers $headers -Body @{
    mode = "pve"
}
$runId = $started.activeRun.id
$offer = @($started.currentOfferCardIds)
Assert-True (-not [string]::IsNullOrWhiteSpace($runId)) "Run start did not return an active run id."
Assert-True ($offer.Count -eq 3) "Opening offer must contain exactly 3 cards."
Assert-True ((@($offer | Select-Object -Unique)).Count -eq 3) "Opening offer contains duplicate cards."
Assert-True ((@($offer | Where-Object { $rarityByCardId[$_] -ne "Legendary" })).Count -eq 0) "Opening offer must contain only Legendary cards."

Write-Host "[draft] POST /runs/draft-state twice"
$stateOne = Invoke-JsonPost -Path "/runs/draft-state" -Headers $headers -Body @{ runId = $runId }
$stateTwo = Invoke-JsonPost -Path "/runs/draft-state" -Headers $headers -Body @{ runId = $runId }
Assert-True ((@($stateOne.currentOfferCardIds) -join "|") -eq ($offer -join "|")) "Draft-state changed the opening offer."
Assert-True ((@($stateTwo.currentOfferCardIds) -join "|") -eq ($offer -join "|")) "Repeated draft-state returned a different offer."

$invalidCardId = $rarityByCardId.Keys |
    Where-Object { $_ -notin $offer -and $_ -ne "Master" } |
    Select-Object -First 1
Assert-Rejected `
    -Path "/runs/select-draft-card" `
    -Headers $headers `
    -Body @{ runId = $runId; cardId = $invalidCardId; pickIndex = 0 } `
    -ExpectedStatusCode 400 `
    -ExpectedErrorCode "card_not_in_draft_offer"

$copyCounts = @{}
for ($pickIndex = 0; $pickIndex -lt 33; $pickIndex++) {
    Assert-True ($offer.Count -eq 3) "Offer $pickIndex must contain exactly 3 cards."
    Assert-True ((@($offer | Select-Object -Unique)).Count -eq 3) "Offer $pickIndex contains duplicate cards."

    if ($pickIndex -eq 0) {
        Assert-True ((@($offer | Where-Object { $rarityByCardId[$_] -ne "Legendary" })).Count -eq 0) "Opening offer contains a non-Legendary card."
    }
    else {
        Assert-True ((@($offer | Where-Object { $rarityByCardId[$_] -eq "Legendary" })).Count -eq 0) "Offer $pickIndex contains a Legendary card after the opening pick."
    }

    $selectedCardId = $offer[0]
    $response = Invoke-JsonPost -Path "/runs/select-draft-card" -Headers $headers -Body @{
        runId = $runId
        cardId = $selectedCardId
        pickIndex = $pickIndex
    }

    $copyCounts[$selectedCardId] = 1 + [int]($copyCounts[$selectedCardId])
    $copyLimit = if ($pickIndex -eq 0) { 1 } else { 3 }
    Assert-True ($copyCounts[$selectedCardId] -le $copyLimit) "Card '$selectedCardId' exceeded copy limit $copyLimit."
    Assert-True ((@($response.draftPickCardIds)).Count -eq ($pickIndex + 1)) "Server returned the wrong pick count after pick $pickIndex."

    if ($pickIndex -eq 0) {
        Assert-Rejected `
            -Path "/runs/select-draft-card" `
            -Headers $headers `
            -Body @{ runId = $runId; cardId = $selectedCardId; pickIndex = 0 } `
            -ExpectedStatusCode 409 `
            -ExpectedErrorCode "stale_draft_pick_index"
    }

    if ($pickIndex -lt 32) {
        Assert-True (-not $response.isComplete) "Draft completed before 33 picks."
        $offer = @($response.currentOfferCardIds)

        if ($pickIndex -eq 4) {
            Write-Host "[draft] Re-authenticate with Game ID and restore the same draft state"
            $resumedAuth = Invoke-JsonPost -Path "/auth/login" -Body @{
                gameId = $GameId
                password = $Password
                clientVersion = $ClientVersion
            }
            Assert-True `
                (-not [string]::IsNullOrWhiteSpace($resumedAuth.sessionToken)) `
                "Game ID re-authentication did not return a new session token."
            Assert-True `
                ($resumedAuth.account.id -eq $auth.account.id) `
                "Game ID re-authentication returned a different account."

            $headers = @{
                Authorization = "Bearer $($resumedAuth.sessionToken)"
            }
            $resumedState = Invoke-JsonPost `
                -Path "/runs/draft-state" `
                -Headers $headers `
                -Body @{ runId = $runId }
            Assert-True `
                ((@($resumedState.draftPickCardIds)).Count -eq ($pickIndex + 1)) `
                "Re-authenticated draft state returned the wrong pick count."
            Assert-True `
                ((@($resumedState.currentOfferCardIds) -join "|") -eq ($offer -join "|")) `
                "Re-authenticated draft state returned a different current offer."
        }
    }
    else {
        Assert-True ([bool]$response.isComplete) "Draft did not complete after 33 picks."
        Assert-True ($response.deck.cardCount -eq 33) "Completed server deck does not contain 33 cards."
        Assert-True ((@($response.currentOfferCardIds)).Count -eq 0) "Completed draft still returned a current offer."
    }
}

Assert-Rejected `
    -Path "/runs/save-draft-picks" `
    -Headers $headers `
    -Body @{ runId = $runId; cardIds = @(); currentOfferCardIds = @() } `
    -ExpectedStatusCode 410 `
    -ExpectedErrorCode "client_authoritative_draft_removed"

Write-Host "[draft] Server-authoritative draft smoke test passed. run=$runId deck=$($response.deck.id)"
