# After333 isolated concurrency checks

Run `RunIsolatedLoadChecks.ps1 -ServerOutput <verified server release directory>` with PowerShell. The runner builds this harness offline, owns a new temporary PostgreSQL cluster on 127.0.0.1:15439 and an HTTP/WebSocket server on 127.0.0.1:17339, preserves artifacts, and stops only its own processes. Occupied ports cause an immediate refusal. No live account, token, connection string or endpoint is accepted.

The executable also verifies database host/port/name/user, actual PostgreSQL data_directory, an OS-temp directory boundary, and zero existing accounts before creating anything. Synthetic accounts obtain tickets from isolated server configuration and create their 33-card decks using real registration and draft HTTP endpoints. No direct run/wallet seeding or forged battle results are used.

## Workload

Six waves: 4, 10, 20, 40, 40, 40 simultaneous authenticated clients (154 accounts / 77 matches). Account preparation has separate concurrency 4 and is excluded from combat timing. Every client joins through the real matchmaking queue. Every match performs both mulligans, 12 alternating master attacks and turn ends, one abrupt disconnect/reconnect, then server-authoritative surrender. Both clients consume the state after each measured command. Reconnect asserts original match/seat and unchanged board/hand/resources. No costly summon/death-effect chains, persistent spells, AI search, or renderer workload are simulated.

Join latency is TCP/WebSocket connect through first battle state, including waiting for the paired peer. Command latency is send through both clients receiving a state containing that action. Reconnect latency excludes the wait to detect the dropped connection, measuring connection/rejoin through restored state. Reports provide count/p50/p95/p99/max, not a preset performance SLA. All traffic is loopback on the same machine as server and PostgreSQL; no internet, TLS proxy, mobile network, or geographic latency is included.

After each wave, assert one result receipt per match, exactly two participant results, correct wins/losses for every original account, and an empty session listing after clients close. Sample server-only private/working memory, cumulative CPU time, handles and thread count every 500 ms (PostgreSQL/harness excluded). Observe another 80 seconds after the last disconnect without forced GC, then require no pending result outbox JSON files. Short-run memory trends cannot prove absence of a long-term leak.

IP HTTP rate limits are raised only in the isolated server (auth/account/connect 100000 per window), since every virtual account originates at loopback. WebSocket per-account and message guards remain enabled. This test measures server concurrency behind the IP gate, not production admission limits, brute-force resistance, or internet DDoS capacity. Existing RunAuthorityChecks covers guard rejection and broader persistence correctness.

## Outputs and duration

The runner prints its fresh artifact directory. `checks.log`, `checks.err.log`, `server.out.log`, `server.err.log`, `load-summary.json`, `load-memory.json`, and `sessions-after-wave-N.json` are retained. Expected run is around two minutes on the tested desktop; harness is bounded to eight minutes. Tokens/passwords are never included in result summaries. Do not publish raw server logs, which include synthetic seat tokens.