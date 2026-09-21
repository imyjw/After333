[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$EncryptedBackupPath,
    [Parameter(Mandatory=$true)][string]$MetadataPath,
    [Parameter(Mandatory=$true)][string]$IdentityPath,
    [Parameter(Mandatory=$true)][string]$SourceDatabaseSettings,
    [string]$AgeExePath=(Join-Path $env:LOCALAPPDATA 'After333\Tools\age-v1.3.2\age\age.exe'),
    [string]$PostgresBinPath='C:\Program Files\PostgreSQL\18\bin'
)
# Invoke in a separate powershell.exe process: inherited PG/PROJECT333 settings are cleared.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$utf8=[Text.UTF8Encoding]::new($false)
$dbPort=15449;$dbName='after333_mybox_restore';$dbUser='after333_restore_test'
$privateBase=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'After333\Server'))
$runRoot=Join-Path $privateBase ('RestoreDrills\Mybox_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N'))
$pgData=Join-Path $runRoot 'PgData'
$dump=Join-Path $runRoot 'downloaded-backup.dump'
$pwFile=Join-Path $runRoot 'init-password.tmp'
$pgStarted=$false;$runCreated=$false
$checks=[Collections.Generic.List[string]]::new()
$result=[ordered]@{
    SchemaVersion=1;StartedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');Succeeded=$false
    CleanupSucceeded=$false;DatabasePort=$dbPort;GameServerStarted=$false
    BackupTimeBaselineCompared=$false;ExternalRecoveryKeyCopyVerified=$false
    Limitations=@(
        'Download provenance is supplied by the caller; this tool verifies the supplied file bytes.'
        'No contemporaneous database baseline was supplied; this is not a backup-time row comparison.'
        'The supplied recorded source locale is an assumption, not a locale captured with this backup.'
        'The identity file is used without printing its contents; an externally stored recovery-key copy is not independently verified.'
        'No operating database, game server, permissions/ownership recovery, or whole-host recovery is tested.'
    )
}
function Require([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Pass([string]$Message){$checks.Add($Message);Write-Output ('PASS: '+$Message)}
function AssertOwnPath([string]$Path){
    $full=[IO.Path]::GetFullPath($Path)
    Require ($full.StartsWith(([IO.Path]::GetFullPath($runRoot)+'\'),[StringComparison]::OrdinalIgnoreCase)) 'Artifact path is outside this restore run.'
    if(Test-Path -LiteralPath $full){Require (((Get-Item -LiteralPath $full -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'Restore artifact must not be a reparse point.'}
    return $full
}
function NewPrivateRunDirectory{
    Require (-not(Test-Path -LiteralPath $runRoot)) 'A new restore directory is required.'
    $ancestor=Split-Path -Parent $runRoot
    while($ancestor){
        if(Test-Path -LiteralPath $ancestor){Require (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'Restore directory ancestors must not be reparse points.'}
        $next=Split-Path -Parent $ancestor
        if($next -eq $ancestor){break};$ancestor=$next
    }
    New-Item -ItemType Directory -Path $runRoot|Out-Null
    $script:runCreated=$true
    $userSid=[Security.Principal.WindowsIdentity]::GetCurrent().User
    $systemSid=[Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($userSid);$acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @($userSid,$systemSid)){
        $rule=[Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit),[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $runRoot -AclObject $acl
    $actual=Get-Acl -LiteralPath $runRoot
    Require $actual.AreAccessRulesProtected 'Restore directory ACL must block inherited access.'
    $allowed=@($userSid.Value,$systemSid.Value)
    $rules=@($actual.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
    foreach($rule in $rules){
        if($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow){Require ($allowed -contains $rule.IdentityReference.Value) 'Unexpected principal can access restore artifacts.'}
    }
    foreach($sid in $allowed){
        Require (@($rules|Where-Object{$_.IdentityReference.Value -eq $sid -and $_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and ($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -eq [Security.AccessControl.FileSystemRights]::FullControl -and ($_.InheritanceFlags -band [Security.AccessControl.InheritanceFlags]::ContainerInherit) -ne 0 -and ($_.InheritanceFlags -band [Security.AccessControl.InheritanceFlags]::ObjectInherit) -ne 0}).Count -gt 0) 'Required private artifact ACL was not applied.'
    }
}
function InvokePrivateTool([string]$Tool,[string[]]$Arguments,[string]$Label,[int]$TimeoutMs=120000){
    $quoted=@($Arguments|ForEach-Object{'"'+[regex]::Replace([regex]::Replace($_,'(\\*)"','$1$1\"'),'(\\+)$','$1$1')+'"'})
    $stdoutPath=AssertOwnPath (Join-Path $runRoot ($Label+'.out.log'))
    $stderrPath=AssertOwnPath (Join-Path $runRoot ($Label+'.err.log'))
    $child=Start-Process -FilePath $Tool -ArgumentList $quoted -WorkingDirectory $runRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    try{
        $null=$child.Handle
        if(-not $child.WaitForExit($TimeoutMs)){
            $child.Kill()
            $null=$child.WaitForExit(5000)
            throw ($Label+' timed out; inspect private logs.')
        }
        $child.Refresh()
        Require ($child.ExitCode -eq 0) ($Label+' failed; inspect private logs.')
    }finally{$child.Dispose()}
}
function PgControl([string]$Action){
    $null=AssertOwnPath $pgData
    $arguments=@('-D',$pgData,'-w','-t','20')
    if($Action -eq 'start'){$arguments+=@('-l',(Join-Path $runRoot 'postgres.log'),'-o',('-h 127.0.0.1 -p '+$dbPort))}
    else{$arguments+=@('-m','fast')}
    $arguments+=$Action
    InvokePrivateTool (Join-Path $PostgresBinPath 'pg_ctl.exe') $arguments ('pg-'+$Action) 30000
}
function Sql([string]$SqlText,[string]$Label){
    $sqlPath=AssertOwnPath (Join-Path $runRoot ($Label+'.sql'))
    [IO.File]::WriteAllText($sqlPath,$SqlText,$utf8)
    InvokePrivateTool (Join-Path $PostgresBinPath 'psql.exe') @('-X','-q','-A','-t','-w','-h','127.0.0.1','-p',"$dbPort",'-U',$dbUser,'-d',$dbName,'-v','ON_ERROR_STOP=1','-f',$sqlPath) $Label
    return [IO.File]::ReadAllText((Join-Path $runRoot ($Label+'.out.log'))).Trim()
}
function Snapshot([string]$Label){
    $schemaPath=AssertOwnPath (Join-Path $runRoot ($Label+'-schema.sql'))
    InvokePrivateTool (Join-Path $PostgresBinPath 'pg_dump.exe') @('-w','-h','127.0.0.1','-p',"$dbPort",'-U',$dbUser,'-d',$dbName,'--schema-only','--no-owner','--no-privileges','--file',$schemaPath) ($Label+'-schema')
    # PostgreSQL 18 emits a fresh psql restrict token on each dump; it is not schema state.
    $schema=[regex]::Replace([IO.File]::ReadAllText($schemaPath),'(?m)^\\(?:un)?restrict [^\r\n]*(?:\r?\n|$)','')
    $sha=[Security.Cryptography.SHA256]::Create()
    try{$schemaHash=[BitConverter]::ToString($sha.ComputeHash($utf8.GetBytes($schema))).Replace('-','')}finally{$sha.Dispose()}
    $data=Sql $fingerprintSql ($Label+'-data')|ConvertFrom-Json
    Require ($data.InvalidConstraints -eq 0) 'Restored database contains an unvalidated constraint.'
    Require ($data.InvalidIndexes -eq 0) 'Restored database contains an invalid or unready index.'
    Require (@($data.Tables).Count -gt 0) 'Restored database contains no user tables.'
    $snapshot=[ordered]@{SchemaSha256=$schemaHash;Data=$data}
    $serialized=$snapshot|ConvertTo-Json -Depth 12 -Compress
    [IO.File]::WriteAllText((AssertOwnPath (Join-Path $runRoot ($Label+'-snapshot.json'))),$serialized,$utf8)
    return [pscustomobject]@{Serialized=$serialized;TableCount=@($data.Tables).Count;SequenceCount=@($data.Sequences).Count}
}
$fingerprintSql=@'
create temporary table restore_table_fingerprints(schema_name text,table_name text,relation_kind text,row_count bigint,row_hash text);
create temporary table restore_sequence_states(schema_name text,sequence_name text,last_value bigint,is_called boolean);
do $drill$
declare t record;
begin
 if exists(select 1 from pg_class c join pg_namespace n on n.oid=c.relnamespace where c.relkind='f' and n.nspname !~ '^pg_' and n.nspname<>'information_schema') then
  raise exception 'Foreign tables are unsupported in this isolated drill';
 end if;
 for t in select n.nspname,c.relname,c.relkind from pg_class c join pg_namespace n on n.oid=c.relnamespace where c.relkind in ('r','p','m') and n.nspname !~ '^pg_' and n.nspname<>'information_schema' order by n.nspname,c.relname loop
  execute format('insert into restore_table_fingerprints select %L,%L,%L,count(*),md5(coalesce(string_agg(to_jsonb(r)::text,E''\n'' order by to_jsonb(r)::text collate "C"),'''')) from %I.%I r',t.nspname,t.relname,t.relkind,t.nspname,t.relname);
 end loop;
 for t in select n.nspname,c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace where c.relkind='S' and n.nspname !~ '^pg_' and n.nspname<>'information_schema' order by n.nspname,c.relname loop
  execute format('insert into restore_sequence_states select %L,%L,last_value,is_called from %I.%I',t.nspname,t.relname,t.nspname,t.relname);
 end loop;
end $drill$;
select json_build_object(
 'Tables',coalesce((select json_agg(t order by schema_name,table_name) from restore_table_fingerprints t),'[]'::json),
 'Sequences',coalesce((select json_agg(t order by schema_name,sequence_name) from restore_sequence_states t),'[]'::json),
 'InvalidConstraints',(select count(*) from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname !~ '^pg_' and n.nspname<>'information_schema' and not c.convalidated),
 'InvalidIndexes',(select count(*) from pg_index i join pg_class c on c.oid=i.indrelid join pg_namespace n on n.oid=c.relnamespace where n.nspname !~ '^pg_' and n.nspname<>'information_schema' and (not i.indisvalid or not i.indisready or not i.indislive))
);
'@
try{
    foreach($inputPath in @($EncryptedBackupPath,$MetadataPath,$IdentityPath,$SourceDatabaseSettings,$AgeExePath)){
        Require (Test-Path -LiteralPath $inputPath -PathType Leaf) 'A required input file or age executable is missing.'
    }
    foreach($toolName in @('initdb.exe','pg_ctl.exe','createdb.exe','pg_restore.exe','pg_dump.exe','psql.exe')){
        Require (Test-Path -LiteralPath (Join-Path $PostgresBinPath $toolName) -PathType Leaf) 'A required PostgreSQL executable is missing.'
    }
    $metadata=Get-Content -Raw -LiteralPath $MetadataPath|ConvertFrom-Json
    Require ($metadata.SchemaVersion -eq 1 -and $metadata.Status -eq 'CopiedToMyboxDrive') 'Unsupported or unsuccessful backup metadata.'
    Require ($metadata.FileName -match '^After333-DB-[0-9]{8}-[0-9]{6}-[a-f0-9]{8}\.dump\.age$' -and [IO.Path]::GetFileName($EncryptedBackupPath) -ceq $metadata.FileName) 'Downloaded backup filename does not match its metadata.'
    Require ($metadata.EncryptedSha256 -match '^[a-fA-F0-9]{64}$' -and $metadata.PlaintextSha256 -match '^[a-fA-F0-9]{64}$') 'Invalid backup checksum metadata.'
    Require ((Get-Item -LiteralPath $EncryptedBackupPath).Length -eq [long]$metadata.EncryptedBytes -and [long]$metadata.EncryptedBytes -gt 0) 'Downloaded encrypted backup size mismatch.'
    Require ((Get-FileHash -LiteralPath $EncryptedBackupPath -Algorithm SHA256).Hash -eq $metadata.EncryptedSha256) 'Downloaded encrypted backup hash mismatch.'
    $sourceLocale=Get-Content -Raw -LiteralPath $SourceDatabaseSettings|ConvertFrom-Json
    Require ($sourceLocale.encoding -eq 'UTF8' -and $sourceLocale.provider -eq 'c' -and -not [string]::IsNullOrWhiteSpace($sourceLocale.collate) -and -not [string]::IsNullOrWhiteSpace($sourceLocale.ctype) -and -not [string]::IsNullOrWhiteSpace($sourceLocale.timezone)) 'Recorded libc UTF8 locale and timezone settings are required.'
    # No production configuration or credentials are imported, and children inherit only drill PG settings.
    foreach($entry in @(Get-ChildItem Env:|Where-Object{$_.Name -like 'PG*' -or $_.Name -like 'PROJECT333_*'})){
        [Environment]::SetEnvironmentVariable($entry.Name,$null,'Process')
    }
    $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$dbPort)
    try{$probe.Start()}finally{$probe.Stop()}
    NewPrivateRunDirectory
    $result['PrivateArtifacts']=$runRoot
    $result['BackupFileName']=$metadata.FileName
    $result['EncryptedSha256']=$metadata.EncryptedSha256
    $result['PlaintextSha256']=$metadata.PlaintextSha256
    $result['RecordedLocaleAssumption']=$sourceLocale
    InvokePrivateTool (Join-Path $PostgresBinPath 'pg_restore.exe') @('--version') 'postgres-version'
    Require ([IO.File]::ReadAllText((Join-Path $runRoot 'postgres-version.out.log')) -match 'PostgreSQL\) 18(?:[.\s]|$)') 'PostgreSQL 18 tools are required.'
    InvokePrivateTool $AgeExePath @('--version') 'age-version'
    Require ([IO.File]::ReadAllText((Join-Path $runRoot 'age-version.out.log')).Trim() -eq 'v1.3.2') 'The recorded official age 1.3.2 tool is required.'
    Pass 'Downloaded ciphertext filename, size and SHA256 match its metadata; private ACL verified'
    Require (-not(Test-Path -LiteralPath $dump)) 'Refusing to overwrite a plaintext dump.'
    InvokePrivateTool $AgeExePath @('--decrypt','--identity',$IdentityPath,'--output',$dump,$EncryptedBackupPath) 'decrypt'
    Require ((Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash -eq $metadata.PlaintextSha256) 'Decrypted plaintext hash mismatch.'
    Pass 'Downloaded ciphertext decrypts to the recorded plaintext SHA256'
    $password=[guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N')
    $env:PGPASSWORD=$password;$env:PGCONNECT_TIMEOUT='5';$env:PGOPTIONS='-c timezone='+$sourceLocale.timezone
    [IO.File]::WriteAllText((AssertOwnPath $pwFile),$password,$utf8)
    InvokePrivateTool (Join-Path $PostgresBinPath 'initdb.exe') @('-D',$pgData,'-U',$dbUser,'--auth-local=scram-sha-256','--auth-host=scram-sha-256',('--pwfile='+$pwFile),'--encoding=UTF8','--text-search-config=simple',('--lc-collate='+$sourceLocale.collate),('--lc-ctype='+$sourceLocale.ctype)) 'initdb' 30000
    Remove-Item -LiteralPath (AssertOwnPath $pwFile)
    PgControl 'start';$pgStarted=$true
    InvokePrivateTool (Join-Path $PostgresBinPath 'createdb.exe') @('-w','-h','127.0.0.1','-p',"$dbPort",'-U',$dbUser,$dbName) 'createdb'
    $isolation=Sql "select json_build_object('data',current_setting('data_directory'),'port',current_setting('port'),'db',current_database(),'user',current_user,'tables',(select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname !~ '^pg_' and n.nspname<>'information_schema' and c.relkind in ('r','p','m','f')));" 'isolation'|ConvertFrom-Json
    Require (([IO.Path]::GetFullPath($isolation.data) -eq [IO.Path]::GetFullPath($pgData)) -and $isolation.port -eq "$dbPort" -and $isolation.db -eq $dbName -and $isolation.user -eq $dbUser -and $isolation.tables -eq 0) 'Fresh database isolation check failed.'
    Pass 'New password-protected cluster and empty loopback database confirmed'
    $restoreTimer=[Diagnostics.Stopwatch]::StartNew()
    InvokePrivateTool (Join-Path $PostgresBinPath 'pg_restore.exe') @('-w','-h','127.0.0.1','-p',"$dbPort",'-U',$dbUser,'-d',$dbName,'--single-transaction','--exit-on-error','--no-owner','--no-privileges',$dump) 'restore'
    $restoreTimer.Stop();$result['RestoreSeconds']=[Math]::Round($restoreTimer.Elapsed.TotalSeconds,2)
    Pass 'Archive restored into the isolated empty database in one transaction'
    $before=Snapshot 'before-restart'
    $result['UserTableCount']=$before.TableCount;$result['SequenceCount']=$before.SequenceCount
    Pass 'Every user table is readable; all constraints and indexes are valid'
    PgControl 'stop';$pgStarted=$false
    PgControl 'start';$pgStarted=$true
    $after=Snapshot 'after-restart'
    Require ($before.Serialized -ceq $after.Serialized) 'Schema, table counts/hashes or sequence state changed across PostgreSQL restart.'
    Pass 'Restart preserves the exact schema, table counts/hashes and sequence state'
    $result['Succeeded']=$true
}catch{
    $result['Failure']=$_.Exception.Message
    Write-Warning 'MYBOX restore validation failed. Inspect the compact result and private logs.'
}finally{
    $cleanupOk=$true
    if($runCreated){
        if($pgStarted -or (Test-Path -LiteralPath (Join-Path $pgData 'postmaster.pid'))){
            try{PgControl 'stop';$pgStarted=$false}catch{$cleanupOk=$false;Write-Warning 'This run could not stop its isolated PostgreSQL cluster.'}
        }
        foreach($temporaryPath in @($dump,$pwFile)){
            try{$safePath=AssertOwnPath $temporaryPath;if(Test-Path -LiteralPath $safePath){Remove-Item -LiteralPath $safePath -Force}}catch{$cleanupOk=$false;Write-Warning 'This run could not remove one of its plaintext temporary files.'}
        }
    }
    foreach($key in @('PGPASSWORD','PGCONNECT_TIMEOUT','PGOPTIONS')){[Environment]::SetEnvironmentVariable($key,$null,'Process')}
    $password=$null
    $result['CleanupSucceeded']=$cleanupOk
    $result['Succeeded']=($result['Succeeded'] -and $cleanupOk)
    $result['Checks']=$checks.ToArray()
    $result['FinishedAtUtc']=[DateTimeOffset]::UtcNow.ToString('O')
    if($runCreated){
        $resultPath=AssertOwnPath (Join-Path $runRoot 'Result.json')
        [IO.File]::WriteAllText($resultPath,($result|ConvertTo-Json -Depth 8),$utf8)
        Write-Output ('Private result: '+$resultPath)
    }else{Write-Output ($result|ConvertTo-Json -Depth 8)}
}
if(-not $result['Succeeded']){exit 1}
