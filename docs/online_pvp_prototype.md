# Online PvP Prototype

## Target Direction

The online PvP target is moving from a local custom-room smoke test toward account-based matchmaking and reconnectable 1:1 battles.

For the short verified LAN demo flow, see `docs/lan_pvp_demo_checklist.md`.
For production-like server setup and public-test readiness checks, see `docs/deployment_readiness_checklist.md`.
For HTTPS/WSS reverse proxy setup, see `docs/https_wss_reverse_proxy_setup.md`.

Target rules:

- PvP clients must be authenticated with a server account before entering matchmaking.
- Guest accounts are valid server accounts.
- Game ID and social login identities must attach to `auth_identities`.
- Matchmaking must associate a queued player with `account_id`, not only a temporary player token.
- The server owns matchmaking, battle seats, command validation, battle resolution, and battle results.
- The server stores language-neutral structured combat-log records as part of the reconnectable battle snapshot.
- Unity converts those structured records into localized sentences and owns the combat-log UI.
- A reconnecting client must recover its active match from account identity and server-side match-player state.
- The existing custom-room tester remains useful for transport and command smoke tests only.

Required future storage:

- account identities and sessions from `account_persistence_db_design.md`
- PvP match queue rows
- PvP match rows
- PvP match-player rows
- connection/reconnect state rows
- battle snapshot and/or command-log rows for reconnect/server-restart recovery

Current persistence progress:

- `0006_pvp_matchmaking_schema.sql` now creates `pvp_match_queue`, `pvp_matches`, `pvp_match_players`, and `pvp_match_connections`.
- `0007_pvp_battle_snapshots.sql` now creates `pvp_battle_snapshots` and `pvp_battle_command_log`.
- `PvpMatchmakingService` records matchmaking joins, match seats, active connections, battle start, disconnect/reconnect-grace state, and match completion into those tables.
- PvP match completion is recorded from the authoritative battle result even when draft-run result metadata is missing, and completion clears reconnect deadlines plus closes persisted active match connections.
- For new account-backed PvP queue joins, `PvpMatchmakingService` now assigns the `matchId` from persisted `pvp_matches` rows before the runtime `BattleSession` is created or joined.
- `/pvp/reconnect-status` now checks persisted reconnect-grace rows and only reports a reconnect button when the persisted match row and the live in-memory battle session agree on the same reconnectable match.
- When a disconnected player reconnects successfully and no players remain disconnected, the persisted match status returns from `reconnect_grace` to `active`.
- `PvpBattlePersistenceService` records battle snapshots after battle start, accepted client commands, server AI actions, turn-timeout actions, reconnect joins, and disconnect forfeit resolution.
- `PvpBattlePersistenceService` records accepted and rejected client commands into `pvp_battle_command_log`, including the command payload, sender identity, and produced battle events when available.
- Battle snapshots now include a restorable domain-state payload, not only player-facing `StateView` data.
- Battle snapshots retain the latest 33 structured combat-log records. A new or reconnecting connection receives the retained history; later `StateView` messages carry sequence-numbered increments.
- If `/pvp/reconnect-status` finds a persisted reconnect-grace match but the live in-memory session is missing, it now reports reconnect availability only when a restorable latest battle snapshot exists.
- When a reconnecting PvP client joins a match that is no longer in memory, the server attempts to restore the `BattleSession` from the latest persisted battle snapshot before seating the reconnecting account.
- Server-startup battle recovery is available but disabled by default for the current graduation-project development flow.
- Set `PROJECT333_ENABLE_PVP_STARTUP_RECOVERY=1` to make server startup move persisted `active` or `reconnect_grace` PvP matches into reconnect-grace recovery because previous WebSocket connections belonged to the old server process.
- When enabled, startup recovery closes stale persisted active connections and marks recoverable player seats as disconnected with the configured reconnect grace period, while preserving still-valid existing reconnect deadlines.
- After one account restores a match from snapshot, later reconnecting accounts can import their persisted reconnect reservation into the already-restored in-memory `BattleSession`.
- The runtime `BattleSession` still owns live command execution after restoration.
- The laptop-hosted development server can now run behind Cloudflare Tunnel at `https://api.after333.com`, while ASP.NET remains bound to `http://127.0.0.1:7333`.
- `PROJECT333_PUBLIC_SERVER_URL` separates the public client endpoint from the internal listen address, and `/server/status` reports both values plus `Deployment.Mode=ReverseProxy` for this setup.
- The next practical verification step is a two-account external-network PvP smoke test through HTTPS/WSS; VPS hosting and managed PostgreSQL remain deferred until the project is closer to release.

## Current Milestone

The current milestone verifies that the Unity client can connect to the local PvP WebSocket server, join a test match, receive a server-authored battle state view, and send basic battle commands that the server resolves.

The server now owns an in-memory `BattleState` for each full two-player test match.
The currently supported server-resolved commands are:

- `PlayUnitCard`
- `PlayBuildingCard`
- `MoveOccupant`
- `Attack`
- `EndTurn`

## Start The Local Server

```powershell
dotnet run --project C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj
```

Default endpoints:

```text
HTTP: http://127.0.0.1:7333/
Health: http://127.0.0.1:7333/health
Server Status: http://127.0.0.1:7333/server/status
WebSocket: ws://127.0.0.1:7333/battle
Sessions: http://127.0.0.1:7333/sessions
```

Use `/health` for a lightweight "is the server process alive?" check.
Use `/server/status` for a fuller development diagnostic that reports server time, version, listen URL, DB configuration/reachability, applied migration count, cards.json schema version/card count, reconnect grace seconds, startup recovery setting, verbose transport logging state, and optional client compatibility hints.
The Unity start scene also calls `/server/status` and shows a compact server line in the account info panel, for example `Server: OK  DB: OK  Cards: 18  Deploy: CHECK`.
If the server sets `PROJECT333_REQUIRED_CLIENT_VERSION` or `PROJECT333_RECOMMENDED_CLIENT_VERSION`, the Unity start scene appends `Ver: OK` or `Ver: CHECK` to that line. `PROJECT333_REQUIRED_CLIENT_VERSION` is also enforced when a client joins an online battle: the Unity WebSocket client sends `X-Project333-Client-Version`, and the server rejects `JoinMatch` with `client_version_mismatch` if the value does not exactly match the required version.
The Unity client's default development build version is defined in `Assets/Project333/Runtime/Presentation/Project333ClientBuildInfo.cs`. Change `Project333ClientBuildInfo.CurrentClientVersion` when cutting a new test build, and start the server with the same value through `-RequiredClientVersion`. Existing scene fields that still contain the old `unity-dev` value are treated as a legacy alias for the central version.
The same `/server/status` response includes a `Deployment` block. In local development it is normal for `Deployment.Status` to be `Check` because `PROJECT333_PVP_SERVER_URL` defaults to `127.0.0.1`; before public testing, review `Deployment.Warnings` and replace local-only settings with reachable server settings.

## Unity Client Server Endpoint Settings

Unity scenes still expose local default server URLs in the Inspector, but runtime network calls now go through `Project333ServerEndpointSettings`.
The legacy local Inspector value is interpreted as an environment default rather than a forced override.

Unity Editor defaults:

```text
HTTP: http://127.0.0.1:7333
Battle WebSocket: ws://127.0.0.1:7333/battle
```

Built After333 player defaults:

```text
HTTP: https://api.after333.com
Battle WebSocket: wss://api.after333.com/battle
```

A custom non-local Inspector URL remains unchanged in both environments. Command-line or environment-variable overrides have the highest priority, followed by saved `ServerSettingsPanel` overrides, then the Inspector/environment default.

Runtime override keys:

```text
Project333.Server.HttpUrl
Project333.Server.BattleWebSocketUrl
```

If `Project333.Server.HttpUrl` is set, account, run, draft, reward, owned-card APIs, and the default battle WebSocket target use that server.
If `Project333.Server.BattleWebSocketUrl` is set, battle matchmaking and reconnect use that URL.
If a WebSocket override is omitted, the battle WebSocket is derived from the HTTP URL by replacing `http` with `ws`, `https` with `wss`, and appending `/battle`.

The game start scene now materializes a `ServerSettingsPanel` in the hierarchy. Use it to edit the HTTP server URL without changing code:

- `Local` stores the local development URL.
- `Test` stores the Inspector-configured test URL, usually `http://{server-pc-ip}:7333` for LAN testing.
- `Apply` saves the typed URL into PlayerPrefs and derives the battle WebSocket endpoint from it.
- `Reset` clears runtime overrides and returns to the environment default: local in the Unity Editor and `api.after333.com` in a built player.

When the saved URL changes, the Unity client clears the saved account session and reconnects against the selected server so tokens from one server are not accidentally reused against another server.

For same-PC two-client PvP testing, the built Unity player supports scoped client profiles:

```powershell
After333.exe -project333Profile PlayerA -project333ServerUrl http://127.0.0.1:7333
After333.exe -project333Profile PlayerB -project333ServerUrl http://127.0.0.1:7333
```

Each profile gets separately scoped credential-store keys for account tokens and separately scoped PlayerPrefs keys for server endpoint overrides. In the Unity Editor and current Windows development player, the credential-store implementation also uses PlayerPrefs; Android and iOS builds use their platform secure stores. Without profiles, two clients on the same Windows user share the same saved account state.

After making a Windows build, use the launcher script:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\LaunchTwoClients_Local.ps1 -ClientExePath 'C:\Path\To\After333.exe'
```

Before switching Unity to a LAN or external server, run the endpoint smoke test from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl 'http://SERVER_PC_IP:7333'
```

The script checks `/health`, `/server/status`, and a raw WebSocket connection to `/battle`.

For a LAN test server, start the server PC with:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Test.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

`RunServer_Test.ps1` prints one or more `Client Server URL` lines, for example:

```text
Client Server URL: http://192.168.0.12:7333
```

Use that URL in the Unity start scene `ServerSettingsPanel`, then press `Apply`.
On another PC, verify the server before launching PvP:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl 'http://192.168.0.12:7333'
```

If the endpoint test fails from another PC but works on the server PC, allow inbound TCP port `7333` in Windows Defender Firewall on the server PC.

## Create The Unity Test Object

In Unity:

```text
Tools > Project333 > Online > Create Connection Tester
```

This creates an `OnlineBattleConnectionTester` object in the active scene.
For a two-client local test, use:

```text
Tools > Project333 > Online > Create Two Local Testers
```

This creates or updates `OnlineBattleConnectionTester_PlayerA` and `OnlineBattleConnectionTester_PlayerB` with the same local match id and different player tokens.
In this one-scene smoke test, only `OnlineBattleConnectionTester_PlayerA` presents server `StateView` messages to the actual battle UI.
`OnlineBattleConnectionTester_PlayerB` still connects, receives messages, and logs ACK/StateView output, but it does not overwrite the shared `BattleScreenPresenter`.
If a `BattleBootstrapper` exists in the active scene, the same menu also configures it to send player UI actions through `OnlineBattleConnectionTester_PlayerA`.
In that mode, hand-card play, occupant movement, attacks, and end turn are sent to the server; the battle UI updates only when the server returns a new `StateView`.

## Test Connection

With the `OnlineBattleConnectionTester` object selected:

1. Enter Unity Play Mode.
2. Open the component context menu.
3. Choose `Connect`.
4. Check the Unity Console for a joined-match log.
5. Choose `Send End Turn Test Command`.
6. Check the Unity Console for ACK logs.
7. Check the server console for the received command.
8. Choose `Disconnect` when finished.

Expected server log shape:

```text
After333 PvP test server listening on http://127.0.0.1:7333
[match:local-test] joined connection=... seat=Player runtimeSeat=Player token=player-a connections=1
[match:local-test] joined connection=... seat=AI runtimeSeat=AI token=player-b connections=2
[match:local-test] Battle started for match local-test.
[match:local-test] command #1 EndTurn actor=player-a/Player runtimeActor=Player connection=...
[match:local-test] Ack events=TurnEnded,TurnStarted -> 2 client(s)
[match:local-test] StateView -> 2 client(s)
```

By default, the server hides raw WebSocket payloads and per-client send logs so the console focuses on match joins, battle start, commands, battle events, `StateView` broadcasts, turn timers, disconnect grace, and forfeit results.
If low-level transport debugging is needed, start the server with `PROJECT333_VERBOSE_TRANSPORT_LOGS=1`.

The Unity test client sends `JoinMatch` immediately after `Connect`.
The test server groups clients by `matchId`. If two Unity clients use the same `matchId`, both clients should show the joined-room message and each ACK is broadcast to every connected client in that match.

When the second client joins, the server starts a prototype battle automatically and sends a filtered `StateView` to each client.
After the active player sends `EndTurn`, the server resolves the command, advances through the next turn-start step, and sends fresh `StateView` messages to both clients.
The server currently resolves unit/building play, damage spells, persistent resource spells, scripted spells, movement, attacks, and end turn through the shared battle rules model.
The prototype deck starts with `Goblin`, `firebolt`, and `Daehwandan` so basic unit and spell commands can be tested quickly.

The Unity test component also exposes these context-menu smoke commands:

- `Send Play Goblin Test Command`
- `Send Move Goblin Test Command`
- `Send Master Attack Test Command`
- `Send Firebolt Enemy Master Test Command`
- `Send End Turn Test Command`

Use them in that order on the active local tester after both local testers have joined.
The Firebolt command targets the enemy Master at `(2,1)` and is intended to verify damage-spell command serialization, server resolution, single-target spell VFX, and damage number presentation.
Victory/Defeat, StateView end state, and Draft-scene return flow must be checked through normal lethal damage or surrender.

## Prototype Seat Assignment

The local test server assigns seats automatically when a client joins a match:

- first active connection in a match: `Player`
- second active connection in a match: `AI`
- third active connection in the same match: rejected with `match_full`

For now, `Player` and `AI` are reused as two PvP seats because the local battle model already uses those two owner ids.
The server rejects commands whose `ActorId` does not match the sender's assigned seat.
The Unity test log prefix includes `playerToken/seat`, for example:

```text
[Project333 Online Test:player-a/Player]
[Project333 Online Test:player-b/AI]
```

Expected `StateView` log shape:

```text
StateView viewer=Player turn=1 active=Player phase=Main hand=4 opponentHand=3 occupants=2 effects=0 myMasterHp=333 opponentMasterHp=333
StateView viewer=AI turn=2 active=AI phase=Main hand=4 opponentHand=4 occupants=2 effects=0 myMasterHp=333 opponentMasterHp=333
```

## Current UI Integration

The real battle scene can now send player UI actions to the local WebSocket test server.
When online input routing is enabled, the client does not mutate the local battle state directly; it waits for the server-authored `StateView` and presents that view.
