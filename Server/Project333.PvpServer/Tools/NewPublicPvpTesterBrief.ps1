param(
    [string]$ServerUrl = "http://SERVER_HOST:7333",
    [string]$ClientVersion = "0.1.0-dev",
    [string]$TestLabel = "",
    [string]$OutputDirectory = "C:\Project_333\Logs\TesterBriefs",
    [string]$PublishedServerPath = "C:\Project_333\Builds\Server\Project333.PvpServer",
    [string]$Notes = "",
    [switch]$NoZip,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Normalize-HttpUrl {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return "http://SERVER_HOST:7333"
    }

    return $Url.Trim().TrimEnd("/")
}

function ConvertTo-SafeFileName {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "public_pvp_test"
    }

    return ($Value.Trim() -replace '[^a-zA-Z0-9가-힣._-]', '_')
}

function Get-PublishManifestSummary {
    param([string]$PublishedServerPath)

    $manifestPath = Join-Path $PublishedServerPath "project333_server_publish_manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return [pscustomobject]@{
            Path = $manifestPath
            Exists = $false
            ClientVersion = ""
            ServerAssemblyVersion = ""
            GitCommit = ""
            CreatedAtUtc = ""
        }
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        return [pscustomobject]@{
            Path = $manifestPath
            Exists = $true
            ClientVersion = "$($manifest.ClientVersion)"
            ServerAssemblyVersion = "$($manifest.ServerAssemblyVersion)"
            GitCommit = "$($manifest.Git.Commit)"
            CreatedAtUtc = "$($manifest.CreatedAtUtc)"
        }
    }
    catch {
        return [pscustomobject]@{
            Path = $manifestPath
            Exists = $false
            ClientVersion = ""
            ServerAssemblyVersion = ""
            GitCommit = ""
            CreatedAtUtc = ""
        }
    }
}

$ServerUrl = Normalize-HttpUrl $ServerUrl
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$safeLabel = ConvertTo-SafeFileName $TestLabel
$briefDirectory = Join-Path $OutputDirectory "${safeLabel}_$timestamp"
$briefPath = Join-Path $briefDirectory "After333_Public_PvP_Tester_Brief_ko.md"
$quickstartSourcePath = "C:\Project_333\docs\public_pvp_tester_quickstart_ko.md"
$manifestSummary = Get-PublishManifestSummary $PublishedServerPath

Write-Host "[tester-brief] ServerUrl=$ServerUrl"
Write-Host "[tester-brief] ClientVersion=$ClientVersion"
Write-Host "[tester-brief] Output=$briefDirectory"
Write-Host "[tester-brief] PublishManifest=$($manifestSummary.Path)"

if ($PrintOnly) {
    Write-Host "[tester-brief] PrintOnly was set. No files were created."
    return
}

New-Item -ItemType Directory -Force -Path $briefDirectory | Out-Null

$manifestLines = if ($manifestSummary.Exists) {
    @(
        "- 서버 manifest: ``$($manifestSummary.Path)``",
        "- 서버 빌드 생성 시각 UTC: ``$($manifestSummary.CreatedAtUtc)``",
        "- 서버 Assembly Version: ``$($manifestSummary.ServerAssemblyVersion)``",
        "- 서버 Git Commit: ``$($manifestSummary.GitCommit)``",
        "- 서버 manifest의 ClientVersion: ``$($manifestSummary.ClientVersion)``"
    )
}
else {
    @(
        "- 서버 manifest: 찾지 못함",
        "- 참고: 서버를 ``PublishServer_Release.ps1``로 publish하면 ``project333_server_publish_manifest.json``이 생성됩니다."
    )
}

$briefLines = @(
    "# After333 공개 PVP 테스트 브리프",
    "",
    "이 파일은 이번 테스트에 필요한 서버 주소와 확인 항목만 짧게 정리한 안내서입니다.",
    "",
    "## 이번 테스트 정보",
    "",
    "- 서버 주소: ``$ServerUrl``",
    "- Health 확인 주소: ``$ServerUrl/health``",
    "- Server Status 확인 주소: ``$ServerUrl/server/status``",
    "- 요구 클라이언트 버전: ``$ClientVersion``",
    "- 테스트 라벨: ``$safeLabel``",
    "",
    "## 서버 빌드 정보",
    ""
)
$briefLines += $manifestLines
$briefLines += @(
    "",
    "## 테스터 실행 순서",
    "",
    "1. After333 빌드 폴더 전체를 받습니다. ``.exe``만 받으면 실행이 깨질 수 있습니다.",
    "2. ``After333.exe``를 실행합니다.",
    "3. 시작 화면의 ``ServerSettingsPanel``에 아래 서버 주소를 입력합니다.",
    "",
    "``````text",
    $ServerUrl,
    "``````",
    "",
    "4. ``Apply``를 누릅니다.",
    "5. 좌상단 상태가 ``Server: OK``, ``DB: OK``, ``Cards: OK``인지 확인합니다.",
    "6. 계정으로 로그인하거나 회원가입합니다.",
    "7. Draft 화면에서 PVP를 눌러 상대와 매칭합니다.",
    "8. 카드 사용, 이동, 공격, 마법, 턴 종료가 양쪽 화면에 동기화되는지 확인합니다.",
    "9. 재접속 테스트는 한쪽 클라이언트를 끄고 60초 안에 다시 켜서 같은 전투로 복귀되는지 확인합니다.",
    "",
    "## 문제가 생기면 보내줄 정보",
    "",
    "- 문제 발생 시간:",
    "- 계정 이름:",
    "- 서버 주소:",
    "- 누른 버튼 또는 카드:",
    "- 화면에 나온 안내 문구:",
    "- 스크린샷/영상 여부:",
    "- 재현 가능 여부:",
    "",
    "## 개발자 메모",
    "",
    $Notes,
    "",
    "자세한 공통 안내서는 함께 첨부된 ``public_pvp_tester_quickstart_ko.md``를 참고하세요."
)

Set-Content -LiteralPath $briefPath -Value $briefLines -Encoding UTF8

if (Test-Path -LiteralPath $quickstartSourcePath) {
    Copy-Item -LiteralPath $quickstartSourcePath -Destination (Join-Path $briefDirectory "public_pvp_tester_quickstart_ko.md") -Force
}

if ($manifestSummary.Exists) {
    Copy-Item -LiteralPath $manifestSummary.Path -Destination (Join-Path $briefDirectory "project333_server_publish_manifest.json") -Force
}

if (-not $NoZip) {
    $zipPath = "$briefDirectory.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $briefDirectory "*") -DestinationPath $zipPath -Force
    Write-Host "[tester-brief] zip=$zipPath"
}

Write-Host "[tester-brief] created=$briefDirectory"
