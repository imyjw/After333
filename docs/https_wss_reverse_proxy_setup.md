# After333 HTTPS/WSS Tunnel And Reverse Proxy Setup

**2026-10-20 운영 기록 갱신:** 서울 Lightsail + Caddy + PostgreSQL18로 이전했고 공개 주소는 `https://api.after333.com`, 운영DB는 `project333_live`다. 사용자 전환 결과에서 migration24개/latest`0024_elf_elder_card_id`를 확인했다. 아래 과거 날짜의 노트북/Tunnel·DB버전·미배포 문구는 당시 기록이다. 현재 실행·백업 절차는 [Lightsail 운영](lightsail_operations.md), 확인 범위와 남은 시험은 [현재 상태](current_project_status.md)를 우선한다. 오늘 원격 재확인을 수행하지 않았다.

After333's ASP.NET server can run as plain HTTP behind a reverse proxy.
The reverse proxy owns public HTTPS/WSS and forwards traffic to the internal `Project333.PvpServer` process.

Legacy laptop development shape (before the Lightsail cutover):

```text
Unity Client
  https://api.after333.com
  wss://api.after333.com/battle
        |
        v
Cloudflare Tunnel
  managed public HTTPS/WSS
        |
        v
Project333.PvpServer
  http://127.0.0.1:7333
        |
        v
PostgreSQL
```

Production now uses Caddy on Seoul Lightsail. The diagram above and the Windows launcher below document the old laptop Tunnel setup; do not run it as the current production path. Caddy manages the production HTTPS certificate and proxies HTTPS/WSS to the loopback origin.

## Legacy Windows Server Command Behind Cloudflare Tunnel

Keep the ASP.NET origin bound to loopback and report the separate public URL:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_CloudflareTunnel.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

This launcher uses:

```text
ListenUrl: http://127.0.0.1:7333
PublicUrl: https://api.after333.com
```

It also disables development smoke commands, enables request rate limits, and enforces client version `0.1.0-dev` by default. Its retained startup-recovery environment setting is legacy compatibility only: current startup cancels unfinished battles without result and cannot revive snapshots.

## Why This Shape

- The game server code can stay simple and keep listening on HTTP.
- Caddy can automatically issue and renew HTTPS certificates for a real domain.
- WebSocket traffic to `/battle` is forwarded by Caddy through the same `reverse_proxy`.
- Port `7333` does not need to be exposed publicly when the server is behind the proxy.

## Files

Caddy template:

```text
C:\Project_333\Server\Project333.PvpServer\Deploy\Caddyfile.example
```

You can copy it manually and replace:

```text
api.after333.com
admin@example.com
```

The recommended repeatable path is to publish the server and generate a complete staging bundle instead:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\PublishServer_Release.ps1 -CleanOutput
```

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\NewStagingDeploymentBundle.ps1 -Domain 'api.after333.com' -AdminEmail 'admin@example.com'
```

The generated bundle contains a ready-to-use `Caddyfile` with the supplied domain and email, but never stores the PostgreSQL connection string.

## Server Command Behind Caddy

When using Caddy, bind the After333 server only to localhost:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Watchdog.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://127.0.0.1:7333' -PublicServerUrl 'https://api.after333.com' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' -RequiredClientVersion '0.1.0-dev'
```

Use `http://0.0.0.0:7333` for LAN direct testing.
Use `http://127.0.0.1:7333` when Caddy is the public entrypoint.

## Caddy Command Example

After installing Caddy and preparing a real Caddyfile:

```powershell
caddy run --config C:\Project_333\Server\Project333.PvpServer\Deploy\Caddyfile
```

The committed file is `Caddyfile.example`, not `Caddyfile`, so local domain/certificate details do not accidentally become project defaults.

## Firewall And Router

For public HTTPS/WSS:

- Allow inbound TCP `80` and `443` to the Caddy machine.
- Do not expose TCP `7333` publicly when using Caddy.
- If testing from a home network, the router must forward `80` and `443` to the server PC.
- A real domain's DNS `A` record must point to the public IP.

For LAN-only HTTP testing, keep using the existing LAN checklist instead:

```text
C:\Project_333\docs\lan_pvp_demo_checklist.md
```

## Unity Client URL

A built After333 client now uses this public endpoint by default:

```text
https://api.after333.com
```

The client derives:

```text
wss://api.after333.com/battle
```

The Unity Editor continues to default to `http://127.0.0.1:7333` for local development. Use `ServerSettingsPanel` only when you want to override the environment default, such as for LAN or staging tests. `Reset` returns to local in the Editor and to `https://api.after333.com` in a built client.

You can still pass an explicit URL to a built client; command-line values take priority over the default and saved UI settings:

```powershell
After333.exe -project333ServerUrl https://api.after333.com
```

## Endpoint Test

After Cloudflare Tunnel and the After333 server are both running, check the actual selected release. The readiness tool updated 2026-10-02 checks running-server reconnect 70 seconds, `StartupRecoveryEnabled=false`, and `InterruptedBattlePolicy=cancel_without_result`, including rejection of missing fields. It does not enable startup snapshot restoration. These source/documentation updates do not themselves verify a live tunnel, certificate or running server.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'https://api.after333.com' -ExpectedPublicUrl 'https://api.after333.com' -ExpectedRequiredClientVersion '0.1.0-dev'
```

Expected:

- `/health` returns `Status=Ok`.
- `/server/status` returns `Status=Ok`, `PublicUrl=https://api.after333.com`, and `Deployment.Mode=ReverseProxy`.
- WebSocket connects to `wss://api.after333.com/battle`.

## Important Notes

- Caddy needs a real reachable domain for automatic public certificates.
- Self-signed or internal certificates can work only if the client PC trusts that certificate authority.
- For current school/LAN demos, direct HTTP is still simpler and acceptable.
- For public internet tests, use HTTPS/WSS.
