[CmdletBinding()]
param([string]$ConfigPath = (Join-Path $env:LOCALAPPDATA 'After333\Server\CloudBackup\settings.json'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$backupHome = Split-Path -Parent $ConfigPath
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$runRoot = Join-Path $backupHome ('Runs\' + $runId)
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$lock = $null
$oldPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD','Process')
$privateFiles = @()
$result = [ordered]@{SchemaVersion=1;RunId=$runId;StartedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');Status='Running';CloudService='NAVER MYBOX';CloudServerConfirmation='NotChecked'}

function Invoke-BackupTool {
    param([string]$Tool,[string[]]$Arguments,[string]$Label)
    # No credential is passed on a command line. PGPASSWORD is inherited only
    # while the PostgreSQL tools execute in this short-lived backup process.
    $quoted = @($Arguments | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_,'(\\*)"','$1$1\"'),'(\\+)$','$1$1') + '"'
    })
    $child = New-Object Diagnostics.Process
    $child.StartInfo.FileName = $Tool
    $child.StartInfo.Arguments = $quoted -join ' '
    $child.StartInfo.UseShellExecute = $false
    $child.StartInfo.CreateNoWindow = $true
    $child.StartInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $child.StartInfo.RedirectStandardOutput = $true
    $child.StartInfo.RedirectStandardError = $true
    try {
        if(-not $child.Start()){throw "$Label did not start."}
        $stdoutTask = $child.StandardOutput.ReadToEndAsync()
        $stderrTask = $child.StandardError.ReadToEndAsync()
        if(-not $child.WaitForExit(1200000)) {
            $child.Kill()
            $child.WaitForExit()
            throw "$Label timed out."
        }
        [IO.File]::WriteAllText((Join-Path $runRoot ($Label+'.out.log')),$stdoutTask.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $runRoot ($Label+'.err.log')),$stderrTask.GetAwaiter().GetResult())
        if($child.ExitCode -ne 0){throw "$Label failed (exit $($child.ExitCode)); inspect protected local logs."}
    } finally { $child.Dispose() }
}

try {
    $lock = [IO.File]::Open((Join-Path $backupHome 'backup.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if($config.SchemaVersion -ne 1){throw 'Unsupported backup configuration.'}
    $cloudRoot = [IO.Path]::GetPathRoot($config.CloudDirectory)
    if(-not(Test-Path -LiteralPath $cloudRoot)){throw 'MYBOX drive is unavailable; no upload was attempted.'}
    if($config.CloudDirectory -ne 'N:\개인\After333-Backups'){throw 'Unexpected cloud destination; review configuration before use.'}
    if($config.Recipient -notmatch '^age1[a-z0-9]+$'){throw 'Invalid encryption recipient.'}
    $age = Join-Path $config.AgeDirectory 'age.exe'
    $pgDump = Join-Path $config.PostgresBinPath 'pg_dump.exe'
    $pgRestore = Join-Path $config.PostgresBinPath 'pg_restore.exe'
    foreach($requiredPath in @($age,$pgDump,$pgRestore,$config.IdentityPath)) {
        if(-not(Test-Path -LiteralPath $requiredPath -PathType Leaf)){throw 'A required backup tool or recovery identity is missing.'}
    }
    $vault = Import-Clixml -LiteralPath (Join-Path $env:LOCALAPPDATA 'After333\Server\DeploymentSettings-20260909.clixml')
    $db = New-Object System.Data.Common.DbConnectionStringBuilder
    $db.set_ConnectionString([Net.NetworkCredential]::new('', $vault['PROJECT333_DB_CONNECTION']).Password)
    $dbHost = [string]$db.get_Item('Host'); $dbPort = [string]$db.get_Item('Port'); $dbName = [string]$db.get_Item('Database'); $dbUser = [string]$db.get_Item('Username')
    if($dbHost -ne '127.0.0.1' -or $dbPort -ne '5432' -or $dbName -ne 'project333'){throw 'Unexpected database target.'}
    $env:PGPASSWORD = [string]$db.get_Item('Password')
    $dump = Join-Path $runRoot 'project333.dump'
    $roundTrip = Join-Path $runRoot 'roundtrip.dump'
    $privateFiles = @($dump,$roundTrip)
    Invoke-BackupTool $pgDump @('-w','-h',$dbHost,'-p',$dbPort,'-U',$dbUser,'-d',$dbName,'-F','c','-f',$dump) 'pg-dump'
    [Environment]::SetEnvironmentVariable('PGPASSWORD',$oldPassword,'Process')
    $db.Clear(); $vault = $null
    if((Get-Item -LiteralPath $dump).Length -le 0){throw 'Empty backup archive.'}
    # Parse the complete archive, not only its table of contents. SQL is sent to
    # NUL; no restore command is executed against any database.
    Invoke-BackupTool $pgRestore @('--file','NUL',$dump) 'archive-read'
    $plainHash = (Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash
    $fileName = 'After333-DB-' + $runId + '.dump.age'
    $encrypted = Join-Path $runRoot $fileName
    Invoke-BackupTool $age @('--encrypt','--recipient',$config.Recipient,'--output',$encrypted,$dump) 'encrypt'
    Invoke-BackupTool $age @('--decrypt','--identity',$config.IdentityPath,'--output',$roundTrip,$encrypted) 'decrypt-check'
    if((Get-FileHash -LiteralPath $roundTrip -Algorithm SHA256).Hash -ne $plainHash){throw 'Encryption round-trip hash mismatch.'}
    $encryptedHash = (Get-FileHash -LiteralPath $encrypted -Algorithm SHA256).Hash
    $result['ArchiveFullyReadable'] = $true
    $result['EncryptionRoundTripVerified'] = $true
    $result['PlaintextSha256'] = $plainHash
    $result['EncryptedSha256'] = $encryptedHash
    $result['EncryptedBytes'] = (Get-Item -LiteralPath $encrypted).Length
    $result['FileName'] = $fileName
    New-Item -ItemType Directory -Path $config.CloudDirectory -Force | Out-Null
    $cloudPath = Join-Path $config.CloudDirectory $fileName
    if(Test-Path -LiteralPath $cloudPath){throw 'Refusing to overwrite an existing cloud backup.'}
    Copy-Item -LiteralPath $encrypted -Destination $cloudPath -ErrorAction Stop
    if((Get-FileHash -LiteralPath $cloudPath -Algorithm SHA256).Hash -ne $encryptedHash){throw 'MYBOX drive copy hash mismatch.'}
    $result['CloudPath'] = $cloudPath
    $result['Status'] = 'CopiedToMyboxDrive'
    $result['DriveReadbackVerified'] = $true
    # A mounted drive may serve cached bytes. This is not a claim that the
    # remote service has confirmed durable upload or an independent download.
    $result['CompletedAtUtc'] = [DateTimeOffset]::UtcNow.ToString('O')
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $runRoot 'manifest.json') -Destination ($cloudPath+'.json') -ErrorAction Stop
}
catch {
    $result['Status'] = 'Failed'
    $result['Error'] = $_.Exception.Message
    throw
}
finally {
    [Environment]::SetEnvironmentVariable('PGPASSWORD',$oldPassword,'Process')
    foreach($privatePath in $privateFiles){
        $fullPrivatePath = [IO.Path]::GetFullPath($privatePath)
        if($fullPrivatePath.StartsWith(([IO.Path]::GetFullPath($runRoot)+'\'),[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $fullPrivatePath)) {
            Remove-Item -LiteralPath $fullPrivatePath -Force
        }
    }
    $result['FinishedAtUtc'] = [DateTimeOffset]::UtcNow.ToString('O')
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'result.json') -Encoding UTF8
    if($null -ne $lock){
        $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupHome 'latest-result.json') -Encoding UTF8
        $lock.Dispose()
    }
    Write-Output ($result | ConvertTo-Json)
}




