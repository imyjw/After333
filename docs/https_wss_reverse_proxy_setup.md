# After333 HTTPS/WSS Tunnel And Reverse Proxy Setup

After333's ASP.NET server can run as plain HTTP behind a reverse proxy.
The reverse proxy owns public HTTPS/WSS and forwards traffic to the internal `Project333.PvpServer` process.

Current development shape:

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

Caddy remains an alternative for a later VPS deployment. The current laptop-hosted public test path uses Cloudflare Tunnel and does not require router port forwarding or a locally managed TLS certificate.

## Server Command Behind Cloudflare Tunnel

Keep the ASP.NET origin bound to loopback and report the separate public URL:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_CloudflareTunnel.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

This launcher uses:

```text
ListenUrl: http://127.0.0.1:7333
PublicUrl: https://api.after333.com
```

It also disables development smoke commands, enables request rate limits, enables PvP startup recovery, and enforces client version `0.1.0-dev` by default.

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

After Cloudflare Tunnel and the After333 server are both running:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'https://api.after333.com' -ExpectedPublicUrl 'https://api.after333.com' -ExpectedRequiredClientVersion '0.1.0-dev' -RequireStartupRecovery
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
