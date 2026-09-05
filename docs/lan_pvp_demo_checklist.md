# LAN PvP Demo Checklist

This checklist captures the verified LAN PvP demo flow for After333.

Use it before a presentation or local multiplayer test.

## Verified Result

The following flow has been verified:

- A server PC runs the After333 PvP server on the LAN.
- A second PC can open the server health endpoint in a browser.
- A built Unity client on another PC can log in through the server.
- Two clients on the LAN can enter PvP matchmaking.
- The match starts, battle commands sync, and the battle can finish.
- After battle end, clients return to the draft/start flow and run results are reflected.

## 1. Start PostgreSQL

Make sure PostgreSQL is running on the server PC.

The current local development password used by the project is:

```text
dev_password
```

## 2. Start The LAN Server

On the server PC, open PowerShell and run:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Test.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

The script prints one or more client URLs:

```text
Client Server URL: http://192.168.x.x:7333
```

Use this URL on every Unity client.

## 3. Check The Server From Another PC

On the other PC, open this in a browser:

```text
http://SERVER_PC_IP:7333/health
```

Expected result:

```json
{"Name":"After333 PvP Server","Status":"Ok", ...}
```

Then check the port from PowerShell:

```powershell
Test-NetConnection SERVER_PC_IP -Port 7333
```

Expected result:

```text
TcpTestSucceeded : True
```

## 4. If Another PC Cannot Connect

On the server PC, allow inbound TCP `7333`:

```powershell
New-NetFirewallRule -DisplayName "After333 PvP Server 7333" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 7333
```

Check the rule:

```powershell
Get-NetFirewallRule -DisplayName "After333 PvP Server 7333"
```

If browser `/health` and `Test-NetConnection` both pass, the LAN/network path is good.

## 5. Build And Copy The Client

Build the Windows client from Unity.

Copy the full build folder to the other PC, not only the `.exe`.

The folder must include files/folders such as:

```text
After333.exe
After333_Data
UnityPlayer.dll
MonoBleedingEdge
```

Project Settings must allow insecure HTTP for LAN development.
The project currently sets:

```text
insecureHttpOption: 2
```

Without this, the browser may reach the server but the Unity build can still show `Server: OFF`.

## 6. Set The Server URL In The Client

Start the built Unity client.

In the game start scene:

1. Find `ServerSettingsPanel`.
2. Enter the LAN server URL:

```text
http://SERVER_PC_IP:7333
```

3. Press `Apply`.
4. Confirm the left account panel shows:

```text
Server: OK  DB: OK
```

If it shows `Server: OFF`, read the `Error:` text shown on the same line.

## 7. Prepare Two Accounts

Use two different accounts.

Do not use the same account on both clients.

Each account needs:

- server login success
- enough tickets to start or continue a run
- a completed 33-card draft deck

If an account has no deck, draft one first.

## 8. Start LAN PvP

On client A:

1. Press `PVP 매칭`.
2. Wait on the matchmaking overlay.

On client B:

1. Press `PVP 매칭`.
2. Confirm both clients enter the same battle.

## 9. Verify Battle Sync

During the match, verify:

- card play appears on both clients
- movement appears on both clients
- attacks and damage appear on both clients
- turn changes appear on both clients
- turn timer behavior is consistent enough for the current prototype

## 10. Finish The Battle

Finish the match with normal play or the development result buttons.

Expected result:

- winner and loser are resolved by the server
- both clients leave battle cleanly
- Draft/Start flow resumes
- wins/losses are reflected on the correct accounts
- no stale PvP reconnect button remains for the ended match

## Quick Failure Map

- Browser `/health` fails: server URL, server process, firewall, or LAN issue.
- `Test-NetConnection` fails: firewall or network routing issue.
- Browser works but Unity shows `Server: OFF`: check HTTP allow setting and the displayed `Error:` text.
- `Server: OK` but Game Start is disabled: account/ticket/run state issue.
- PvP button unavailable: account needs a completed 33-card deck or run state refresh.
- Same-account warning appears: both clients are logged into the same account.
- Match starts but commands do not sync: check server PowerShell logs and client console/player log.

## Current Confirmed LAN Address Example

The latest successful local test used:

```text
http://192.168.0.10:7333
```

This address can change if the server PC changes networks or receives a new IP from the router.
