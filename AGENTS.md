# After333 agent instructions

## Project and scope

- The active project is C:/Project_333. Use it as the working directory for After333 commands.
- Public name: After333. Preserve internal Project333 namespaces, assets, environment variables, database names, switches and PlayerPrefs keys.
- Current scope includes online accounts, server-owned economy/upgrades/gifts, draft and 33-win/3-loss runs, normal human/AI matchmaking, daily AI challenge and reconnect.
- Do not add ranked/MMR/seasons, paid purchases, new login providers, multi-tile rules or other undefined features without an explicit request and confirmed rules.
- The user builds/installs client apps; do so only when explicitly requested. Server startup automation remains deferred.

## Rule authority and targeted reading

- User decisions take precedence. Never invent missing rules or TBD values; ask only for the unresolved decision and continue independent authorized work.
- Rule-document precedence: docs/battle_rules.md > docs/game_design.md > docs/draft_rules.md > docs/meta_rules.md.
- Before implementation, read the relevant sections of those documents and docs/implementation_contract.md. Use headings/search to select sections; do not load every document by default.
- The extracted contract preserves earlier obligations but cannot override current rule documents. Its legacy whitelist and scope statements have documented exceptions.
- Battle changes: docs/battle_rules.md plus contract sections Battle Gateway Contract, relevant Battle Invariants and Golden Tests v1.
- AI changes: docs/pve_ai.md and the contract's AI Behavior section.
- Cards/upgrades: docs/card_authoring_checklist.md and docs/meta_rules.md; consult card_catalog.md/card_details.md for the affected cards.
- Online modes/reconnect: docs/battle_modes.md and docs/online_pvp_prototype.md.
- Economy retries, gifts or ads: docs/account_operation_retries.md, docs/gift_inbox.md or docs/rewarded_ads.md as relevant.
- docs/special_effects.md is a comparison/reference guide, not a replacement for battle_rules.md.
- docs/archive contains historical evidence, not instructions to preload. When continuing prior development, read docs/codex_handoff.md and the working-preferences section of docs/conversation_decisions.md; retrieve older conversation details only when needed.

## Server and client boundaries

- Accounts, draft offers/picks/decks, upgrades, purchases, rewards and final results are server-authoritative. Unity sends intent and may cache presentation data.
- Presentation uses IBattleGateway; preserve LocalBattleGateway behavior and shared battle services. Do not mutate online battle truth in UI code.
- Filter hidden information per viewer. Identity uses auth_identities; reconnect restores the authenticated account's original seat/run/deck.
- Preserve atomic/idempotent economy operations and durable result delivery; never restore client-authoritative wins or rewards.
- An unfinished battle interrupted by server outage/maintenance is canceled without win/loss. Apply mode-specific challenge refunds from battle_modes.md.

## Unit races

- Unit cards have zero or more races, separate from effects. Ask the user for a new unit races (including none); never infer them from names or art.
- Use docs/unit_races.md for confirmed assignments. Erasure preserves races. Buildings and spells have no unit race field.

## Card upgrades

- Before adding a card, obtain its upgrade eligibility and exact level 1–13 stat/effect increments from the user. Never infer a schedule from card type, rarity or a similar card.
- Reuse already confirmed answers. Do not implement ambiguous levels; continue unrelated work.
- Record accepted schedules in meta/card documentation and verify per-level gains, Lv.13 totals and actual battle values. Change existing cards only when requested.
- Existing explicit schedules: Cerberus HP +1 at Lv.1–12, ATK +1 only at Lv.13; ManaPond HP +1 at Lv.1–13 and no ATK.

## Working practice

- Preserve unrelated working-tree edits; do not commit or push without a request.
- Keep investigations scoped: search first, read matching sections, summarize large logs instead of returning complete logs.
- Use tests that cover changed behavior; preserve relevant Golden Tests. Report what was actually checked and distinguish source changes, client builds and server deployment.
- At a major task boundary, update a short handoff with decisions, remaining work and verification dates. A new task should use the registered Project_333 project at C:/Project_333.
