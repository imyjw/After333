param(
 [Parameter(Mandatory=$true)][string]$BackupRecordDirectory,
 [Parameter(Mandatory=$true)][string]$ServerOutput,
 [Parameter(Mandatory=$true)][string]$SourceDatabaseSettings,
 [string]$PostgresBin='C:\Program Files\PostgreSQL\18\bin'
)
$ErrorActionPreference='Stop'
$utf8=[Text.UTF8Encoding]::new($false)
$sourceLocale=Get-Content -Raw -LiteralPath $SourceDatabaseSettings|ConvertFrom-Json
if($sourceLocale.encoding -ne 'UTF8' -or $sourceLocale.provider -ne 'c' -or [string]::IsNullOrWhiteSpace($sourceLocale.collate) -or [string]::IsNullOrWhiteSpace($sourceLocale.ctype) -or [string]::IsNullOrWhiteSpace($sourceLocale.timezone)){throw 'Recorded libc UTF8 collation and timezone settings are required.'}
$privateBase=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'After333\Server'))
$metadata=Get-Content -Raw -LiteralPath (Join-Path $BackupRecordDirectory 'Backup.json')|ConvertFrom-Json
$backup=[IO.Path]::GetFullPath($metadata.Path)
if(-not $backup.StartsWith($privateBase+'\Backups\',[StringComparison]::OrdinalIgnoreCase)){throw 'Only a recorded private After333 backup is accepted.'}
if((Get-FileHash -LiteralPath $backup).Hash -ne $metadata.SHA256){throw 'Backup hash mismatch.'}
$baseline=Get-Content -Raw -LiteralPath (Join-Path $BackupRecordDirectory 'BackupDatabase.json')|ConvertFrom-Json
$inspectSql=[IO.File]::ReadAllText((Join-Path $BackupRecordDirectory 'InspectDatabase.sql'))
$release=(Resolve-Path -LiteralPath $ServerOutput).Path
$manifest=Get-Content -Raw -LiteralPath (Join-Path $release 'project333_server_publish_manifest.json')|ConvertFrom-Json
foreach($entry in $manifest.PublishedFiles){if((Get-FileHash -LiteralPath (Join-Path $release $entry.RelativePath)).Hash -ne $entry.Sha256){throw 'Server release manifest mismatch.'}}
$runRoot=Join-Path $privateBase ('RestoreDrills\'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $runRoot){throw 'A new private directory is required.'}
$pgData=Join-Path $runRoot 'PgData'
$dbPort=15449;$httpPort=17349;$dbName='after333_restore_drill';$dbUser='after333_restore_test'
$baseUrl="http://127.0.0.1:$httpPort"
foreach($port in @($dbPort,$httpPort)){
 $probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
 try{$probe.Start()}finally{$probe.Stop()}
}
New-Item -ItemType Directory -Path $runRoot|Out-Null
$timer=[Diagnostics.Stopwatch]::StartNew()
$checks=[Collections.Generic.List[string]]::new()
$server=$null;$pgStarted=$false;$success=$false;$failure=$null;$sqlIndex=0;$tableCount=0;$restoreTimer=$null
$savedEnv=@{}
# Production credentials are never imported. Clear inherited settings only in this process.
foreach($item in Get-ChildItem Env:|Where-Object{$_.Name -like 'PROJECT333_*' -or $_.Name -like 'PG*'}){$savedEnv[$item.Name]=$item.Value}
$password=[guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N')
$settings=@{
 PROJECT333_DB_CONNECTION="Host=127.0.0.1;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$password;Timeout=5"
 PROJECT333_PVP_SERVER_URL=$baseUrl
 PROJECT333_PUBLIC_SERVER_URL=$baseUrl
 PROJECT333_REQUIRED_CLIENT_VERSION='0.1.0-dev'
 PROJECT333_ENABLE_PVP_STARTUP_RECOVERY='0'
 PROJECT333_DEV_MIN_TICKETS='-1'
 PROJECT333_NEW_ACCOUNT_TICKETS='3'
 PROJECT333_NEW_ACCOUNT_RESOURCE_GOLD='100'
 PROJECT333_TICKET_PURCHASE_GOLD_COST='3'
 PROJECT333_DRAFT_RUN_TICKET_COST='3'
 PROJECT333_AUDIT_LOG_DIR=(Join-Path $runRoot 'Audit')
 PROJECT333_BATTLE_RESULT_OUTBOX_DIR=(Join-Path $runRoot 'Outbox')
 PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW='600'
 PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW='60'
 PGPASSWORD=$password
 PGCONNECT_TIMEOUT='5'
 PGOPTIONS=('-c timezone='+$sourceLocale.timezone)
 ASPNETCORE_ENVIRONMENT='Production'
 DOTNET_ENVIRONMENT='Production'
}
foreach($key in $settings.Keys){if(-not $savedEnv.ContainsKey($key)){$savedEnv[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}}
function Pass([string]$label){$checks.Add($label);Write-Output "PASS: $label"}
function Require([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function Sql([string]$text){
 $script:sqlIndex++
 $path=Join-Path $runRoot ('query_'+$script:sqlIndex+'.sql')
 [IO.File]::WriteAllText($path,$text,$utf8)
 $answer=& (Join-Path $PostgresBin 'psql.exe') -X -q -A -t -w -h 127.0.0.1 -p $dbPort -U $dbUser -d $dbName -v ON_ERROR_STOP=1 -f $path 2> (Join-Path $runRoot ('query_'+$script:sqlIndex+'.err.log'))
 if($LASTEXITCODE -ne 0){throw "Isolated SQL check $script:sqlIndex failed; inspect private log."}
 return ($answer -join "`n").Trim()
}
function PgControl([string]$action){
 $arguments=@('-D',('"'+$pgData+'"'),'-w','-t','15')
 if($action -eq 'start'){$arguments+=@('-l',('"'+(Join-Path $runRoot 'postgres.log')+'"'),'-o',('"-h 127.0.0.1 -p '+$dbPort+'"'))}else{$arguments+=@('-m','fast')}
 $arguments+=$action
 $p=Start-Process (Join-Path $PostgresBin 'pg_ctl.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot ('pg-'+$action+'.log')) -RedirectStandardError (Join-Path $runRoot ('pg-'+$action+'.err.log'))
 $null=$p.Handle
 if(-not $p.WaitForExit(20000)){throw "Isolated pg_ctl $action timed out."}
 $p.Refresh();if($p.ExitCode -ne 0){throw "Isolated pg_ctl $action failed."}
}
function StartServer([string]$label){
 $script:server=Start-Process 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList @('"'+(Join-Path $release 'Project333.PvpServer.dll')+'"') -WorkingDirectory $runRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot ($label+'.out.log')) -RedirectStandardError (Join-Path $runRoot ($label+'.err.log'))
 for($i=0;$i -lt 50;$i++){
  if($script:server.HasExited){throw 'Isolated server exited during startup.'}
  try{if((Invoke-RestMethod "$baseUrl/health" -TimeoutSec 1).Status -eq 'Ok'){return}}catch{}
  Start-Sleep -Milliseconds 200
 }
 throw 'Isolated server startup timed out.'
}
function StopServer{
 if($null -ne $script:server -and -not $script:server.HasExited){Stop-Process -Id $script:server.Id;if(-not $script:server.WaitForExit(10000)){throw 'Isolated server did not stop.'}}
 $script:server=$null
}
function Api([string]$path,$body=$null,[string]$token=''){
 $headers=@{'X-Project333-Client-Version'='0.1.0-dev'}
 if($token){$headers.Authorization='Bearer '+$token}
 $p=@{Uri=$baseUrl+$path;Headers=$headers;TimeoutSec=10}
 if($null -ne $body){$p.Method='Post';$p.ContentType='application/json';$p.Body=($body|ConvertTo-Json -Depth 10 -Compress)}
 return Invoke-RestMethod @p
}
function ExpectStatus([string]$path,$body,[string]$token,[int]$expected){
 try{$null=Api $path $body $token;throw 'Request unexpectedly succeeded.'}
 catch{if($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $expected){throw}}
}
try{
 foreach($key in $savedEnv.Keys){[Environment]::SetEnvironmentVariable($key,$null,'Process')}
 foreach($key in $settings.Keys){[Environment]::SetEnvironmentVariable($key,$settings[$key],'Process')}
 $pwFile=Join-Path $runRoot 'init-password.tmp'
 [IO.File]::WriteAllText($pwFile,$password,$utf8)
 try{
  $initArgs=@('-D',('"'+$pgData+'"'),'-U',$dbUser,'--auth-local=scram-sha-256','--auth-host=scram-sha-256',('--pwfile="'+$pwFile+'"'),'--encoding=UTF8','--text-search-config=simple',('--lc-collate="'+$sourceLocale.collate+'"'),('--lc-ctype="'+$sourceLocale.ctype+'"'))
  $init=Start-Process (Join-Path $PostgresBin 'initdb.exe') -ArgumentList $initArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'initdb.log') -RedirectStandardError (Join-Path $runRoot 'initdb.err.log')
  $null=$init.Handle
  if(-not $init.WaitForExit(20000)){Stop-Process -Id $init.Id;throw 'Isolated initdb timed out.'}
  $init.Refresh();if($init.ExitCode -ne 0){throw 'Isolated PostgreSQL initialization failed.'}
 }finally{if(Test-Path -LiteralPath $pwFile){Remove-Item -LiteralPath $pwFile}}
 PgControl 'start';$pgStarted=$true
 & (Join-Path $PostgresBin 'createdb.exe') -w -h 127.0.0.1 -p $dbPort -U $dbUser $dbName *> (Join-Path $runRoot 'createdb.log')
 if($LASTEXITCODE -ne 0){throw 'Isolated database creation failed.'}
 $isolation=Sql "select json_build_object('data',current_setting('data_directory'),'port',current_setting('port'),'db',current_database(),'tables',(select count(*) from pg_tables where schemaname='public'));"|ConvertFrom-Json
 Require (([IO.Path]::GetFullPath($isolation.data) -eq [IO.Path]::GetFullPath($pgData)) -and $isolation.port -eq "$dbPort" -and $isolation.db -eq $dbName -and $isolation.tables -eq 0) 'Database isolation check failed.'
 Pass 'Fresh password-protected cluster and empty loopback database; no production connection supplied'
 $restoreTimer=[Diagnostics.Stopwatch]::StartNew()
 # Empty isolated DB is mandatory. Never use --clean, --create or connection termination.
 & (Join-Path $PostgresBin 'pg_restore.exe') -w -h 127.0.0.1 -p $dbPort -U $dbUser -d $dbName --exit-on-error --single-transaction --no-owner --no-privileges $backup *> (Join-Path $runRoot 'restore.log')
 if($LASTEXITCODE -ne 0){throw 'Backup restore failed; inspect private restore.log.'}
 $restoreTimer.Stop()
 Pass 'Recorded backup restored in one transaction, including constraints and indexes'
 $restored=Sql $inspectSql|ConvertFrom-Json
 foreach($field in @('MigrationCount','LatestMigration','Accounts','Runs','Wins','Losses','WalletHash','CollectionHash','RunsHash','TransactionsHash','ReceiptsHash')){Require ($restored.$field -eq $baseline.$field) "Backup-time comparison failed: $field"}
 Require ($restored.CancelConstraints -contains 'PRIMARY KEY (account_id, request_id)') 'Cancellation primary key missing.'
 $restored|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $runRoot 'RestoredBaseline.json') -Encoding UTF8
 Pass 'Schema, counts, results and wallet/collection/run/transaction/receipt hashes match backup-time records'
 Require ((Sql "select count(*) from pg_index i join pg_class c on c.oid=i.indrelid join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and (not i.indisvalid or not i.indisready);") -eq '0') 'Invalid restored indexes.'
 # Exact original records stay inside the private isolated DB, never repository output.
 $snapshotSql=@"
create schema restore_audit;
create table restore_audit.original_rows(table_name text not null,row_data jsonb not null);
do `$`$ declare t record; begin
 for t in select tablename from pg_tables where schemaname='public' order by tablename loop
  execute format('insert into restore_audit.original_rows select %L,to_jsonb(r) from public.%I r',t.tablename,t.tablename);
 end loop;
end `$`$;
create function restore_audit.changed_rows() returns bigint language plpgsql as `$`$
declare t record; delta bigint; total bigint:=0; begin
 for t in select tablename from pg_tables where schemaname='public' loop
  execute format('select count(*) from (select row_data from restore_audit.original_rows where table_name=%L except all select to_jsonb(r) from public.%I r) d',t.tablename,t.tablename) into delta;
  total:=total+delta;
 end loop; return total;
end `$`$;
create function restore_audit.fingerprints() returns table(table_name text,row_count bigint,row_hash text) language plpgsql as `$`$
declare t record; begin
 for t in select tablename from pg_tables where schemaname='public' order by tablename loop
  return query execute format('select %L::text,count(*),md5(coalesce(string_agg(to_jsonb(r)::text,'''' order by to_jsonb(r)::text),'''')) from public.%I r',t.tablename,t.tablename);
 end loop;
end `$`$;
"@
 $null=Sql $snapshotSql
 $fingerprintSql='select json_agg(t order by table_name) from restore_audit.fingerprints() t;'
 $initialPrints=Sql $fingerprintSql
 [IO.File]::WriteAllText((Join-Path $runRoot 'AllTablesBeforeServer.json'),$initialPrints,$utf8)
 $tableCount=[int](Sql "select count(*) from pg_tables where schemaname='public';")
 Require ($tableCount -gt 0) 'Restored database has no public tables.'
 StartServer 'server'
 Require ((Api '/health/db').Status -eq 'Ok') 'Restored server DB health failed.'
 Require ((Sql $fingerprintSql) -ceq $initialPrints) 'Starting the release changed restored public data.'
 Pass "Released server starts without changing any of $tableCount public tables"
 $gameId='restore.'+[guid]::NewGuid().ToString('N').Substring(0,12)
 $accountPassword='Restore_'+[guid]::NewGuid().ToString('N')+'!'
 $auth=Api '/auth/register' @{gameId=$gameId;password=$accountPassword;displayName='Restore drill';clientVersion='0.1.0-dev'}
 $accountId=([guid]$auth.account.id).ToString('D')
 $login=Api '/auth/login' @{gameId=$gameId;password=$accountPassword;clientVersion='0.1.0-dev'}
 Require ($login.account.id -eq $accountId) 'Synthetic login mismatch.'
 $token=$login.sessionToken
 $me=Api '/me' $null $token
 Require ($me.wallet.resourceGold -eq 100 -and $me.wallet.tickets -eq 3) 'Synthetic seed mismatch.'
 Pass 'Synthetic Game ID registration, login and account query work on restored DB'
 $null=Sql "insert into user_card_collection(account_id,card_id,copy_count,upgrade_level) values ('$accountId','Cerberus',10,0),('$accountId','ManaPond',10,0);"
 $purchase=@{requestId=[guid]::NewGuid().ToString('D');ticketCount=1}
 $bought=Api '/wallet/purchase-ticket' $purchase $token
 $repeat=Api '/wallet/purchase-ticket' $purchase $token
 Require ($bought.wallet.resourceGold -eq 97 -and $bought.wallet.tickets -eq 4 -and $repeat.replayed -eq $true -and $repeat.wallet.resourceGold -eq 97) 'Purchase or replay failed.'
 $upgrades=@()
 foreach($card in @('Cerberus','ManaPond')){
  $request=@{requestId=[guid]::NewGuid().ToString('D');cardId=$card;expectedUpgradeLevel=0};$upgrades+=$request
  $upgrade=Api '/cards/upgrade' $request $token
  Require ($upgrade.upgradedCard.upgradeLevel -eq 1 -and $upgrade.upgradedCard.copyCount -eq 7) 'Upgrade level/copy count mismatch.'
  $repeat=Api '/cards/upgrade' $request $token
  Require ($repeat.replayed -eq $true -and $repeat.upgradedCard.upgradeLevel -eq 1) 'Upgrade replay failed.'
 }
 $cancelled=@{requestId=[guid]::NewGuid().ToString('D');ticketCount=1}
 $cancel=Api '/account/operations/resolve' @{requestId=$cancelled.requestId} $token
 Require ($cancel.status -eq 'cancelled') 'Cancellation failed.'
 ExpectStatus '/wallet/purchase-ticket' $cancelled $token 400
 Pass 'Purchase/upgrades commit once; repeated requests and cancelled late purchase cannot charge twice'
 $run=Api '/runs/start' @{mode='pve'} $token
 $runId=$run.activeRun.id;$offer=@($run.currentOfferCardIds)
 Require ($offer.Count -eq 3) 'Draft must give three choices.'
 $state=Api '/runs/draft-state' @{runId=$runId} $token
 Require (($state.currentOfferCardIds -join '|') -ceq ($offer -join '|')) 'Saved offer changed.'
 for($pick=0;$pick -lt 33;$pick++){
  $selection=Api '/runs/select-draft-card' @{runId=$runId;cardId=$offer[0];pickIndex=$pick} $token
  $offer=@($selection.currentOfferCardIds)
 }
 Require ($selection.isComplete -eq $true -and $selection.deck.cardCount -eq 33 -and $selection.activeRun.wins -eq 0 -and $selection.activeRun.losses -eq 0) 'Draft completion mismatch.'
 ExpectStatus '/runs/sync-local-record' @{runId=$runId;wins=33;losses=0} $token 410
 ExpectStatus '/runs/claim-rewards' @{runId=$runId;wins=33} $token 400
 Pass 'Saved draft choices/33-card deck work; forged results and premature rewards are rejected'
 $beforeRestart=Api '/me' $null $token
 Require ($beforeRestart.wallet.resourceGold -eq 91 -and $beforeRestart.wallet.tickets -eq 1) 'Final synthetic wallet mismatch.'
 $beforeRestartPrints=Sql $fingerprintSql
 StopServer
 PgControl 'stop';$pgStarted=$false
 PgControl 'start';$pgStarted=$true
 StartServer 'restart-server'
 Require ((Sql $fingerprintSql) -ceq $beforeRestartPrints) 'Restart changed public data.'
 $afterRestart=Api '/me' $null $token
 Require (($afterRestart|ConvertTo-Json -Depth 30 -Compress) -ceq ($beforeRestart|ConvertTo-Json -Depth 30 -Compress)) 'Account/deck changed across restart.'
 $repeat=Api '/wallet/purchase-ticket' $purchase $token
 Require ($repeat.replayed -eq $true -and $repeat.wallet.resourceGold -eq 91 -and $repeat.wallet.tickets -eq 1) 'Restart purchase replay changed current wallet.'
 foreach($request in $upgrades){
  $repeat=Api '/cards/upgrade' $request $token
  Require ($repeat.replayed -eq $true -and $repeat.upgradedCard.upgradeLevel -eq 1 -and $repeat.wallet.resourceGold -eq 91) 'Restart upgrade replay failed.'
 }
 Require ((Api '/account/operations/resolve' @{requestId=$purchase.requestId} $token).status -eq 'completed') 'Receipt did not survive restart.'
 ExpectStatus '/wallet/purchase-ticket' $cancelled $token 400
 Require ((Api '/account/operations/resolve' @{requestId=$cancelled.requestId} $token).status -eq 'cancelled') 'Cancellation did not survive restart.'
 Pass 'Database and HTTP server restart preserve account/deck, receipts and cancellation; replays do not double-charge'
 Require ((Sql 'select restore_audit.changed_rows();') -eq '0') 'Original restored records were changed.'
 [IO.File]::WriteAllText((Join-Path $runRoot 'AllTablesAfterChecks.json'),(Sql $fingerprintSql),$utf8)
 Pass "All original rows across $tableCount public tables remain unchanged after synthetic API checks"
 $success=$true
}catch{$failure=$_.Exception.Message;throw}
finally{
 $cleanupOk=$true
 try{StopServer}catch{$cleanupOk=$false;Write-Warning 'Isolated server cleanup failed.'}
 if($pgStarted -or (Test-Path -LiteralPath (Join-Path $pgData 'postmaster.pid'))){try{PgControl 'stop'}catch{$cleanupOk=$false;Write-Warning 'Isolated PostgreSQL cleanup failed.'}}
 foreach($key in $savedEnv.Keys){[Environment]::SetEnvironmentVariable($key,$savedEnv[$key],'Process')}
 $settings.Clear();$password=$null;$timer.Stop()
 [pscustomobject]@{
  Succeeded=($success -and $cleanupOk);CleanupSucceeded=$cleanupOk;CheckedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
  BackupSha256=$metadata.SHA256;BackupBytes=$metadata.Bytes;ServerRelease=$release;DatabaseSettings=$sourceLocale
  RestoreSeconds=if($null -ne $restoreTimer){[Math]::Round($restoreTimer.Elapsed.TotalSeconds,2)}else{$null}
  TotalSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,2);PublicTableCount=$tableCount
  Checks=$checks.ToArray();PrivateArtifacts=$runRoot;Failure=$failure
 }|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $runRoot 'Result.json') -Encoding UTF8
 Write-Output "Private result: $runRoot\Result.json"
 if(-not $cleanupOk){throw 'Restore drill cleanup requires attention.'}
}