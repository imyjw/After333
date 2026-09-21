# After333 development handoff

Updated 2026-09-21 for Codex context cleanup. Read this at continuation, then only the documents needed by the new user request.

## Context continuity

- First read the working-preferences section of [conversation_decisions.md](conversation_decisions.md); use its topic table only for the current task.
- That index preserves earlier user decisions, separate deferrals, evidence dates, and the source conversation ID for targeted retrieval. It is not a claim that every old message is remembered automatically.
- Friends-only testing is deferred until after 2026-09-23, with no start date agreed. GitHub Actions, server auto-start and battle-log cleanup have separate holds.
- Do not treat old unresolved entries in cumulative audits as current without reading their later dated updates. Ads are currently not reproduced, not conclusively fixed.

## Location and workflow

- Repository: C:/Project_333; registered Codex project: Project_333.
- Documents/New project is the old prototype. Its AGENTS.md now routes After333 work here.
- Start with AGENTS.md. Detailed contracts are in docs/implementation_contract.md; canonical rule documents outrank that extracted reference.
- The working tree contains extensive user/development changes. Check current git status; preserve unrelated edits and do not assume those changes are committed.
- User builds/installs clients. Server startup automation was explicitly deferred.
- New cards require the user's explicit upgrade eligibility and Lv.1–13 schedule. Do not infer one.

## Last development completed in this conversation

Verified on 2026-09-13, not a claim that the live server is still running today:
- Normal matchmaking prioritizes a person, then starts server AI after 20 seconds in the same room. Counts toward the normal run.
- AI challenge: current run's 33-card deck versus existing AI fixed decks; 3/day at KST midnight; accepted start consumes one; win grants 10 Resource Gold, loss grants zero; no normal-run result changes.
- Only server outage/maintenance cancellation refunds the original day's challenge attempt.
- Common battle reconnect supports both opponent types with 60-second grace.
- Admission disconnect handling avoids an active challenge without initialized battle state.
- Details and rollout evidence: docs/battle_modes.md.
- Then-current validation: server 114 groups plus isolated restart checks; Unity EditMode 1,204 tests and cloned-scene rendering.
- Then-deployed release: Builds/Server/Releases/20260913-battle-modes-verified; schema migrations 0018/0019. Check live health/process state before reporting present deployment status.
- On 2026-09-21 the user confirmed rebuilding and installing the latest client after the 20-second AI matchmaking / daily AI challenge changes. This is user confirmation of installation, not a new device runtime test.

## Other established rules

- Multi-hit applies to eligible counterattacks for every card with the effect.
- SpellPower boosts all Magic damage using the source owner's active total at damage time; no cast-time capture.
- ManaPond: mana cost 5, SpellPower +20; confirmed upgrade schedule is HP +1 at all levels.
- Deck/draft list hold preview and administrator gift inbox were implemented; consult their source/docs if relevant.
- Claimed gifts disappear from the visible inbox; server receipts remain for idempotency.

The harness cleanup is complete. Later feature work is recorded below; use the latest dated entry. Do not rerun historical audits or load the old conversation automatically.

## Operations follow-up (2026-09-21)

- User independently downloaded the 21:00 KST MYBOX encrypted backup and manifest through the website. Local-key decryption and both recorded hashes passed.
- Restored it into a new private PostgreSQL 18 cluster on loopback port 15449. All 34 tables readable; constraints/indexes valid; schema and all row-count/hash/sequence states identical after database restart.
- Stopped the temporary cluster and removed plaintext dump/password files. Production PostgreSQL and game-server PIDs unchanged, server health Ok.
- Tool: Server/Project333.PvpServer/Tools/TestMyboxPostgresRestore.ps1. Evidence: TempBuild/MyboxRestoreDrill_20260921; details/limits: docs/mybox_database_backup.md.
- Remaining operations recovery scope: externally stored key copy, full host/configuration and non-DB pending result journal. The MYBOX database-restore check itself is complete; do not repeat it without a new reason.

## Unit races implemented (2026-09-21)

- User assigned all 21 Unit cards: see docs/unit_races.md. Multiple races and no race allowed; Erasure preserves races. ElfLongbowScout was subsequently changed to Elf at the user request.
- Robot special effect removed from definitions, assets and text; Robot Fusion, Factory, AI and UI targeting use Robot race. Sealbound remains excluded from fusion; survivor keeps its own races.
- JSON and Unity unit authoring expose races; buildings/spells do not. New units require user-confirmed races and the existing explicit upgrade schedule.
- New wire views and persistence snapshots preserve Races; HasRobot remains only as old-input compatibility. Explicit empty races override the legacy flag.
- Verified: final full Unity EditMode 1,238/1,238; server build and offline JSON persistence/view checks passed. Initial whole-suite AI timing-sensitive failure and successful rechecks are documented in unit_races.md.
- No player build/install and no live server deploy/restart for this change. Roll out client and server together. Working tree remains uncommitted; preserve earlier edits.

## Active effects and Elf correction (2026-09-21)

- ElfLongbowScout now has Elf (stable enum value 9), with JSON, Unity asset, tests and race/card/decision documents updated.
- PowerPlant remains named 발전소. Automatic turn-start Gold→Power conversion removed; manual active costs 1 Gold and gains 2 Power, once per plant per owner turn. User confirmed immediate use on summon turn.
- Tile click shows a small 액티브 button only when eligible; button click sends ActivateEffect intent and source RuntimeId. Uses the real tile image rect and a child Canvas above occupant visuals. No manual scene wiring.
- User clarified: all actives are own-turn-only by default; opponent-turn use requires an explicitly described exception and matching implementation. PowerPlant has no exception. Documentation clarified; existing owner-turn guards already enforce this.
- Shared rules enforce owner/Main/cost/alive/not suppressed/not pending fusion/once-per-turn. LastActiveEffectTurnNumber persists through wire views, AI cloning, and server snapshots. AI evaluates the active as a resource action. Active tooltip added.
- Final full Unity EditMode 1,253/1,253 passed, including actual scene copy UI interaction. Server build and offline BattleSession command/snapshot checks passed. No live server deployment, app build/install or Android touch/pixel validation. See docs/active_effects.md.

## Active button affordability (2026-09-21)

- Active buttons now use CanOfferActivation (turn/status/usage) rather than CanActivate (also cost). No-gold PowerPlant stays visible and clickable; click shows 골드가 부족합니다, does not consume usage, and leaves the button for retry. Cost preflight shares service rules; server revalidates independently. AI retains affordability filtering.
- Updated active/battle/special-effect/decision docs. Added no-gold, resource-loss-after-selection and retry coverage.
- Validation: Unity-generated compiler response files compiled runtime and EditMode test assemblies to TempBuild/ActiveButtonCost_20260921 without errors. Isolated server build passed; offline BattleSession checks passed for both seats including no-gold rejection, later success, duplicate rejection and snapshot restoration.
- Unity EditMode tests were compiled but not executed this turn; the project is open in the user editor and no live editor automation connection is configured. No visual/device test, client build/install or server restart/deployment. Initial default-output server build hit a running apphost lock; reran successfully with isolated artifacts output. Do not stop the user editor/server for validation.

## Hero rework (2026-09-21)

- Hero is now 33/33 with Rush; no innate Invincible or turn-end growth. Exact effect: 상대방 소환물을 공격해 파괴할 때마다 공격력 또는 체력+33. Cost 3 mana, Human, Unique, melee Fixed, upgrade schedule and draft/reward exclusions remain unchanged. Lv.13 is 39/42.
- AttackService awards one independent 50/50 +33 ATK or max/current HP per enemy non-Master occupant killed by the Hero normal attack, before destruction-effect chains. Counts Shielder/Piercing victims once; excludes counterattacks, friendly/Master kills and destruction collateral. A Hero dead from simultaneous counterattack gets no bonus. Erasure suppression/reset uses existing rules.
- Server combat logs now append Hero stat gains on attacks, preserving existing wire enum identity. Tooltip only lists Rush; no removed growth keyword. JSON, validator, rule/catalog/detail/authoring/decision docs updated. Hero has no separate Unity card asset (existing JSON-only card).
- Verified 109 pure C# regression cases outside Unity using the actual test methods: Hero, combat, end-turn, multihit counters, piercing, invincible and tooltip tests. Server offline --hero-only checks passed for both seats, combat log and JSON restoration. Unity-generated compiler response files compiled runtime and EditMode test assemblies without errors. Evidence: TempBuild/HeroRework_20260921/{regressions.log,server-hero.log,unity-runtime.log,unity-tests.log}.
- No Unity Test Runner execution or live UI/device validation; no client build/install or server deployment/restart. Use isolated --artifacts-path for server builds while the development server runs. User-owned Editor remains open.

## Nuclear / Fallout (2026-09-21)

- User confirmed Nuclear: Legendary ScienceCivilization spell, Power 10, not upgradeable, draft/rewards disabled. No new card art. See docs/nuclear_fallout.md for exact rules.
- Destroy all non-Master/unsealed occupants on both boards, ignoring Invincible/Endure, with normal destruction/revival effects. Remove the whole group before NuclearPowerPlant explosions.
- Add permanent enemy-board Fallout stack each cast; present/future summons acquire it. Both turn starts deal one Fixed packet of 10 per stack, honoring normal protections/Drained. Erasure and Gu preserve exposure. Masters excluded; sealed summons defer new exposure until release.
- Board stack count and per-occupant exposure by original board owner persist through wire views, both seat projections, AI copies/position keys and server domain JSON snapshots. Re-entering the same board does not duplicate old exposure. New battles reset.
- UI status/tooltip and damage log support added; existing AI simulation supports the spell, future hazard and reactor lethal paths. Card catalog now 43, with authoring/detail/rule/decision docs synchronized.
- Verified 152 actual pure C# test methods/cases outside Unity Editor; offline server --nuclear-only both-seat cast/end-turn/log/full JSON restoration and legacy defaults passed. Unity runtime and EditMode assemblies compile without errors using generated response files. Evidence: TempBuild/Nuclear_20260921. Scoped diff --check passed.
- No Unity Test Runner execution, UI/device verification, client build/install, live server restart or deployment. Preserve all preexisting working-tree changes; no commit/push was made.

## Magic Amplification Circle (2026-09-21)

- User confirmed MagicAmplificationCircle / 마법 증폭진: Common Fantasy spell, mana 1, not upgradeable; excluded from draft (explicit follow-up) and rewards. Same-turn casts stack +10 SpellPower each. No artwork supplied/created, no fixed AI deck changes.
- Uses existing PersistentEffectState per cast and SpellPowerRules.GetTotal, independent of occupant death/suppression. Adds to live Magic damage from all sources and existing HUD total. EndTurnService expires it after owner turn-end effects, before next turn starts (RedDragon benefits; next-turn Firewall does not).
- Existing snapshot/view projection/AI clone paths preserve stacks and expiry without schema changes. Shared AI search validated buff-then-lethal-spell action. Both-seat server actual commands verified stacked casts, mid-turn JSON restore, amplified damage and expiry after end-turn/reconnect.
- 191 related C# regression cases passed outside Unity Editor. Existing SpellPowerTests copied to temporary runner with only Unity Application.dataPath replaced by the real Assets path. Unity runtime + EditMode response-file compilations passed; scoped diff --check passed. Evidence: TempBuild/MagicAmplification_20260921.
- Relevant rule/contract/meta/special-effect/card/authoring/decision docs updated; card catalog now 44. See docs/magic_amplification_circle.md.
- No Unity Test Runner or UI/device verification, app build/install, server deployment/restart, commit or push. Preserve preceding changes.

## Occupant status preview (2026-09-21)

- User confirmed source-specific stat buffs below held card preview; applicable player/field influences above; innate abilities remain left/right. docs/occupant_status_tooltips.md records the full definitions. Releases close every region.
- CombatStatBuffState records Hero kill, RobotFusion, Daehwandan and DemonKing revival bonuses without changing their rules. Same source aggregates; Erasure clears existing attribution, revival rebuilds it, Gu retains it. State views/server JSON/AI cloning preserve it. Old snapshots show only unattributed stat differences, excluding account upgrades and current HP loss.
- OccupantStatusTooltipCatalog splits attached statuses/current dynamic bonuses from applicable terrain/player influences; durations, stacks, suppression and viewer-local ownership are live. TileTextView uses pooled panels and CardPreviewTooltipLayout; same-content skips re-layout, server replacement updates immediately, in-place changes poll at 150ms while holding. Effects grouped by source; no hidden automatic paging.
- Verified 245 pure C# cases outside Unity (source effects, status tooltip contents and layout geometry included). Offline server --status-tooltips-only both-seat JSON/legacy/copy/Erasure checks pass. Unity runtime+EditMode response-file compilation passes. UI Canvas/TMP tests compiled but not executed in Unity; actual phone readability remains unverified. Evidence: TempBuild/StatusTooltips_20260921.
- No client build/install, Unity scene/prefab edits, server restart/deployment, commit or push. Update both client and server for source attribution. Very small views with exceptional effect counts fall back to overall scaling; device visual check needed.

## Compact occupant status labels (2026-09-21)

- User refined held-preview display: above/below show names and applicable numeric values only. Remove descriptions and category prefixes; left/right innate ability explanations remain.
- Examples: 용사: 공격력 +33, 체력 +33; 웨어울프: 공격력 +10; 버서커: 공격력 +12; 주문력 +30; 마법 증폭진: 주문력 +20. 방전/망각/환술/낙진/천라지망/무적 are names only, with no countdown prose.
- TileTextView disables empty body objects and measures compact title-only panels. Source grouping, live updates, expiry, range filtering and release-to-hide remain. No battle rules, source attribution or server schema changes.
- Updated occupant_status_tooltips.md, battle_rules.md, special_effects.md and conversation_decisions.md. This entry supersedes earlier detailed-body/duration display wording.
- Verified 245/245 pure C# checks outside Unity, Unity runtime/EditMode response-file compilation and scoped diff --check. One initial failure was an old DemonKing description assertion; updated to the new empty-body contract and all passed.
- Evidence: TempBuild/CompactStatusTooltips_20260921. Actual Unity UI/device execution not performed; no client build/install, server restart/deploy or commit/push.

## Daehwandan attribution deployment diagnosis (2026-09-21)

- User saw 능력치 변화: 공격력 +30 after Daehwandan. Current SpellService already calls ApplyCombatStatBuff("Daehwandan", +30), and factory/projector/catalog preserve its source.
- Read-only live-host evidence: Project333.PvpServer PID 17432 started 22:10:48 KST; its bin/Debug/net8.0 DLL timestamp is 22:10:47. That DLL contains CastDaehwandan but neither CombatStatBuffState nor ApplyCombatStatBuff metadata strings. Source attribution change in SpellService was written at 23:13:55. The running server predates attribution support.
- Extended CombatStatBuffTests to cast the real spell and verify JSON roundtrip, both viewer projections and exact master tooltip 대환단: 공격력 +30, before/after the independent Qi effect expires. 246/246 pure checks passed; Unity runtime/EditMode compilation passed. Evidence: TempBuild/DaehwandanAttribution_20260921.
- No production logic patch was necessary; do not relabel all unknown stat gains as Daehwandan. Server must be rebuilt/restarted for new battles to send source attribution. Earlier battles with no source records cannot reconstruct provenance reliably.
- No restart/deploy or app build/install performed. Coordinate restart because it cancels unfinished battles under the user-confirmed rule; do not assume the recorded PID persists into a later task.

## A-301 focus-fire investigation (2026-09-21)

- User asked why AI master switches from HP2 A-301 to HP3 A-301. Investigation only: no gameplay code or server deployment changed.
- AiPositionEvaluator values surviving equal ranged units linearly by HP (0.55 base + 0.30 ranged per HP). Nonlethal HP3→2 and HP2→1 attacks therefore receive equal scores when other factors match. It has no explicit progress-to-future-kill preference. AiDecisionService searches up to three same-turn commands through opponent turn start; it does not plan the next AI turn. Equal scores keep the first candidate, with fixed coordinate order.
- Controlled probe: three unupgraded A-301 at columns1,2,3 front row with one HP2, player upkeep funded, AI hand empty, default AI master. Gave player master Always Invincible solely to exclude direct-master attack from this comparison; this is not asserted to match the user's entire field.
- All three candidate attack scores were identical; center-column2 was selected whether it had HP3 or HP2. Reproduces switching away from the injured flank target. Full original board/log was not captured.
- Evidence and standalone probe: TempBuild/AiFocusInvestigation_20260921/{Program.cs,probe.log}. Proposed future change: modest focus-fire / effective attacks-to-kill preference among comparable safe targets, preserving immediate lethal, threat, counterattack and destruction-effect priorities.

## Equivalent-unit focus fire (2026-09-22)

- User chose a small preference for lower current HP among units whose other stats/status are equal, rather than forecasting attacks-to-kill on future turns.
- AiDecisionService now uses target HP only as a secondary decision for equal primary scores (epsilon0.0001), with same initial attacker and equivalent non-Master Unit targets. Best-node choice and beam ordering share this rule. Ordinary scores/costs, candidate order, hidden-information rules and simulation/time limits unchanged.
- AiBattleStateCopy factors its existing occupant key serialization for reuse without changing PositionKey output; comparison omits HP/position and adds actual Attack/current invincibility/exact HuanShu/revival state. Same card, maximumHP, statuses, races, resource/upkeep and buff provenance must match.
- Requires intended target damage/removal in both existing first-action simulations; excludes HuanShu attacker, immediate/projected terminal results and target-linked persistent effects. Active area-damage applicability must also match.
- A-301 hp2 vs hp3 reproduction now selects hp2 at columns1/2/3 (before: center by fixed tie order). Added15 regression cases. All185 pure AI checks passed outside Unity, including prior lethal/counter/destruction/hazard/hidden-state/budget checks. Unity runtime+EditMode compiler response checks passed. Independent review found no blocking issue.
- Evidence: TempBuild/AiFocusFire_20260922/{probe.log,tests.log,build.log,unity-runtime.log,unity-tests.log}. Two preexisting tests use temporary copies replacing only Unity Application.dataPath for the pure runner. No actual Unity Test Runner/device execution.
- Updated pve_ai.md and conversation_decisions.md. No client build/install, server restart/deploy, commit or push. Online use requires latest server rebuild/restart; prior restart question remains unanswered. Do not infer permission to interrupt an active battle.

## Tile click suppression investigation (2026-09-22)

- User asked to verify intermittent PC tile/attack clicks, reportedly since Sep13 19:08 continuation. Investigation only; production scripts unchanged.
- Confirmed source-control-flow bug: TileLongPressRelay sets its own suppression and TileView's suppression on preview, but consumes only its own on release click. Next genuine click is swallowed by TileView. Exit/disable without a click leaves both flags, swallowing two later clicks.
- Deterministic probe compiles unchanged actual two source files with minimal engine/preview boundary stubs. Normal3 clicks=>3 events; hold-release+3 shortclicks=>2; hold-exit/disable+3=>1. Not a live Unity EventSystem/device replay. See docs/tile_click_investigation_2026-09-22.md and TempBuild/TileClickInvestigation_20260922.
- Exact Sep13 19:08:57 continuation was verification/deployment/docs, no input runtime edit. Current two source files are unchanged from HEAD; same bug exists in commit186041c (June8). Do not attribute it to token interruption.
- No fix/build/install/server restart yet. User answered 잘 모르겠음 when asked whether affected clicks followed preview; symptom correlation remains unconfirmed. Mobile remains unverified. Other non-preview click failures not established.


## Tile preview click fix (2026-09-22)

- User authorized the confirmed preview-related click fix only and will report remaining symptoms.
- Removed TileView's duplicate pending click flag/API; TileLongPressRelay suppresses only its own preview gesture and resets suppression on each new pointer-down, before artwork/occupancy checks. Up/exit/disable retain same-gesture release protection.
- Six source-control-flow regression scenarios passed using actual production scripts with minimal Unity/preview stubs. Added matching six Unity EditMode cases; runtime and test assemblies compile successfully. Evidence: TempBuild/TileClickFix_20260922.
- Actual Unity Test Runner and PC/mobile UI not exercised; do not claim all intermittent click causes resolved. Client rebuild needed; no app build/install, server restart/deploy, commit/push performed. See docs/tile_click_investigation_2026-09-22.md.
