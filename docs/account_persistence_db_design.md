# Account Persistence DB Design

**2026-10-20 운영 기록 갱신:** 서울 Lightsail + Caddy + PostgreSQL18로 이전했고 공개 주소는 `https://api.after333.com`, 운영DB는 `project333_live`다. 사용자 전환 결과에서 migration24개/latest`0024_elf_elder_card_id`를 확인했다. 아래 과거 날짜의 노트북/Tunnel·DB버전·미배포 문구는 당시 기록이다. 현재 실행·백업 절차는 [Lightsail 운영](lightsail_operations.md), 확인 범위와 남은 시험은 [현재 상태](current_project_status.md)를 우선한다. 오늘 원격 재확인을 수행하지 않았다.

실제 적용 상태(2026-10-09): 사용자 요청으로 운영 DB에 `0023_ai_challenge_run_usage` 적용 완료(총23개/latest0023). 적용 전 전체 백업과 아카이브 읽기를 검증했고, 기존33개 테이블의 행 수·전체 행 해시 및 도전25건이 보존되었다. 이번에는 서버 배포·재시작이나 클라이언트 빌드·설치를 수행하지 않았다. 상세 [운영 적용 기록](battle_modes.md#운영-db-마이그레이션-적용-2026-10-09).

실제 적용 상태(2026-10-02): 아래 소스 대조의 운영 미조회 설명은 배포 전 기록이다. 후속 별도 승인으로 운영 DB0020/0021/0022·카드팩 서버 적용 및 Android APK(versionCode5) 빌드를 완료했고, 실제 스키마는 총22개/latest0022로 확인했다. [실제 적용 기록](shop_card_packs.md)에31보호 테이블 보존 및 대기열2행의 검증된 시작 시 상태 변경을 구분해 기록했다. 실기기 실행은 미검증이다.

## 현재 구현과 설계 예시의 구분 (2026-10-21 소스 확인)

현재 소스에는 SQL 마이그레이션 `0001`~`0025`가 있다. `0025_single_login_family.sql`은 한 계정의 최신 독립 로그인만 유지하는 family 연결과 기존 세션 변환을 추가한다. 0025는 운영 미적용이며 마지막 사용자 확인 운영 DB는 24개/latest0024다. 상세 계약·검증·적용 조건은 [단일 로그인](single_device_login.md)을 따른다. `0020_card_upgrade_costs_revision.sql`은 2026-10-01 확정 비용표의 13개 전이를 설정한 기록으로 유지하고, `0021_card_upgrade_level2_cost_revision.sql`은 2026-10-02 확정한 Lv.1→Lv.2의 동일 카드·계정 골드 비용만 6/6으로 갱신한다. 이후 `0022_card_pack_purchase_receipts.sql`은 영수증 작업 종류에 `card_pack_purchase`를 추가한다. `0023_ai_challenge_run_usage.sql`은 계정·런별 비환급 골드 던전 도전 조회 인덱스만 추가하며 기존 기록을 초기화하거나 재정산하지 않는다. 계정·인증/자동 로그인·서버 드래프트·강화·런 보상·광고 티켓·티켓/카드팩 구매와 강화의 요청 영수증·취소·관리자 선물함·골드 던전·사람 우선 매칭/AI 전환이 구현되어 있다. 카드팩은 33계정 골드 차감과 10장 지급을 한 트랜잭션에서 확정하고, 개봉 전에도 이미 보유카드에 포함한다. [카드팩 계약](shop_card_packs.md)을 따른다. 실제 DB의 적용 버전과 운영 실행본은 별도 점검 대상이며 이번 문서 대조에서 조회하지 않았다.

문서의 `Suggested fields`와 미구현 미개봉 팩 재고/개별 카드 상점/제작은 설계 예시다. 실제 스키마는 `Server/Project333.PvpServer/Persistence/Migrations`와 서비스 코드가 기준이다. 게스트 및 계정 연결 경로는 현재 제공하지 않으며 Game ID 또는 Google 로그인과 저장 세션 복구를 사용한다. 시작 씬 톱니바퀴 메뉴에는 확인 후 로그아웃이 있다. 로그아웃은 로컬 자격 증명을 지우고 로그인 게이트를 다시 표시하며 계정 소유 데이터는 삭제하지 않는다.

실행 중 서버의 사람/AI/도전 재접속은 70초다. 서버 재시작으로 미완료 전투는 취소하며, 영속 전투 결과 저널의 확정 결과만 멱등 재처리한다. 골드 던전은 티켓 3개로 시작한 현재 진행 중 런의 완성된 덱으로 런당 최대 3회·승리 Resource Gold10이고 일반 런 성적과 별개다. 자정·재로그인은 초기화하지 않고 서버 취소는 원래 런에만 환급한다. 현재 런의 기존 도전도 날짜와 무관하게 합산하며 3회 이상이면 잔여 0회다. `challenge_day`는 감사 정보로 유지한다. 광고 티켓의 UTC 일일 한도와 혼동하지 않는다.

## Document Purpose

This document defines the database design for accounts, login identities, owned card copies, card upgrade levels, drafted run decks, run progress, reward history, and account-backed PvP reconnection.

This is an implementation contract for server-owned account data. Battle rules still live in the battle-rule documents; account progression and login state must stay behind the server API/DB layer.

Related documents:

- Battle rules: [battle_rules.md](./battle_rules.md)
- Draft rules: [draft_rules.md](./draft_rules.md)
- Meta rules: [meta_rules.md](./meta_rules.md)
- Online PvP prototype: [online_pvp_prototype.md](./online_pvp_prototype.md)
- PostgreSQL implementation plan: [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md)

## Confirmed Product Direction

Confirmed design direction:

- Login supports first-party Game ID login.
- Social login targets are Naver, Kakao, and Google.
- Google login is implemented; Naver and Kakao remain future targets.
- Guest account creation and authentication are disabled.
- The game requires a valid Game ID or social-login account before any game feature can be used.
- User-facing identity linking is not exposed. The start-scene settings menu provides confirmed logout and then the mandatory login gate; account data remains server-owned.
- Account and meta progression are designed for an actual server DB, not client-local authoritative storage.
- Local data is only temporary cache or demo data unless a later rule says otherwise.
- Static card definitions stay outside user rows.
- User card ownership is stored as card copies plus an upgrade level.
- Card upgrades are similar to Clash Royale style progression.
- Upgrade costs consume owned copies of that same card plus an out-of-battle currency.
- A drafted deck is used until that run reaches either 33 wins or 3 losses.
- A run deck stores card ids only; each battle applies the account's current upgrade level for those cards.
- PvE and PvP can use the same persisted deck/run schema.
- Normal run battles use human-first matchmaking with server AI fallback after 20 seconds. Per-run Gold Dungeon is an independent mode; see [battle_modes.md](battle_modes.md).
- Normal-run Resource Gold/card-copy rewards are granted by a one-time run-end claim. Gold Dungeon wins separately grant Resource Gold10; verified ads grant tickets and accepted gifts grant their server-owned amounts.
- Run rewards grant eligible card copies directly. The confirmed 33 Resource Gold card pack also grants 10 copies immediately on purchase commit; opening is presentation only. See [shop_card_packs.md](shop_card_packs.md).
- Future acquisition routes beyond the confirmed run rewards and card-pack purchases may include individual-card shop inventory and crafting.
- Tickets are spent when starting a new run.
- PvP matchmaking and reconnect must use authenticated server accounts, not local-only player names.
- A reconnecting client must prove account identity with a valid `sessionToken` or a freshly restored session.
- Each started battle retains a server-owned participant snapshot containing account, runtime/online seat, run, deck contents and upgrade levels. Reconnect requests restore these values; they cannot replace them or attach a run to a practice battle.
- Participant snapshots persist separately from live connections, including when all participants disconnect. Recovery restores participant metadata before constructing upgrade providers. Legacy snapshots may use identities from their server-saved connections; a missing historical participant cannot be reconstructed from new client input, so that seat's recovery is rejected.
- Started `pvp_match_players` entries reject changes to account, run, deck or runtime seat. Run-result updates also require the bound account to own the run and the completed deck to match.
- Migration `0012_battle_result_receipts` adds `battle_result_receipts` (result UUID primary key, match ID, complete server result JSON, recorded timestamp) and `battle_run_results` (result UUID + seat primary key, account/run/deck and win flag). The receipt and all run/match updates share one transaction. Duplicate identical delivery succeeds without another increment; conflicting reuse of an ID is rejected.
- BattleSession generates its result UUID independently of the client-visible room name and persists it in snapshots. Result extraction is non-destructive. Legacy active snapshots derive a stable server-side identity; legacy ended snapshots without result identity are not automatically replayed because prior application cannot be proven.
- Before a terminal response, BattleSession writes its immutable result to the server result outbox using a flushed temporary file and atomic rename. The hosted worker retries pending files after DB recovery and loads them again on restart. Files are removed only after DB commit; replay after an uncertain commit is safe because of the receipt.
- `PROJECT333_BATTLE_RESULT_OUTBOX_DIR` may select a persistent private server directory. The default is `LocalApplicationData/After333/Server/BattleResults/<database fingerprint>`; the fingerprint uses host, port, database and username, excluding credentials. Keep the directory across releases and back it up with server state. It is server-owned data, never a client upload directory.
- Malformed, conflicting or permanently rejected results are retained and logged for investigation. Retry delays grow from 5 to 60 seconds. This does not recover a terminal result that was never durably written, nor protect against simultaneous loss of the DB and server outbox disk.

## Scope

This document is a design contract.

Current implementation target:

- mandatory Game ID or Google login
- automatic restoration through session and refresh tokens
- Game ID login/register support
- social-login-ready identity storage for Google, Kakao, and Naver
- account-backed draft/run/reward/card-upgrade persistence
- confirmed 33 Resource Gold card-pack purchase, immediate 10-card grant and recoverable opening presentation
- account-backed PvP matchmaking/reconnect storage

Still deferred unless explicitly requested:

- production OAuth provider integration
- real-money purchase flows beyond the confirmed Resource Gold ticket/card-pack purchases
- server-owned unopened pack inventory, cross-device presentation markers and purchase-history UI beyond the current local opening presentation
- crafting

## High-Level Storage Rule

Card definitions and user ownership are separate.

- Static card data stays in `cards.json` or a future server-side card-definition table.
- User-specific data goes into the database.
- A user's collection stores `card_id` references, not copied card definitions.
- A user's collection stores copy count and upgrade level per `card_id`.
- Drafted run decks store the selected card ids for the run.
- Live battle command execution uses the in-memory server `BattleSession`.
- PvP snapshots and accepted/rejected command-log rows are persisted for account-based reconnect and optional server-startup recovery.

## Server DB Direction

The target architecture uses an actual server DB.

Recommended DB:

- PostgreSQL

The Unity client must not be the authoritative owner of account progression, card collection, upgrades, tickets, rewards, or run results.

For local development, the backend server and PostgreSQL can still run on the developer machine. The important rule is that persistence flows through the server layer instead of Unity writing authoritative progression directly.

Advantages:

- Server can validate account ownership, rewards, tickets, and upgrades.
- Works with real login.
- Works across devices.
- Safer against cheating.
- Better fit for PvP matchmaking, rewards, packs, shop, and future live features.

Limitations:

- Requires backend hosting or a local backend process during development.
- Requires migrations and operational discipline.
- Requires PostgreSQL setup for realistic persistence tests.

Best use:

- Real account persistence.
- PvP-linked progression.
- Reward and economy systems.
- Any data that should not be trusted from the client.

### Non-Authoritative Local Cache

Local files or local SQLite may still be used only as a non-authoritative cache.

Allowed uses:

- Keeping registered-account session/refresh credentials only through [AccountCredentialStore](mobile_secure_credential_storage.md); obsolete guest tokens are deleted, not resumed.
- Caching account/profile data for UI startup.
- Caching card images or static data.
- Temporary demo-only data when the backend is not being used.

Not allowed as authoritative data:

- Card ownership.
- Card upgrade levels.
- Resource Gold.
- Tickets.
- Run wins/losses.
- Run-end rewards.
- Pack contents.

## Offline Play Data

"Offline play data" means progression data created while the game is not connected to the server.

Examples:

- An old local-only prototype profile stored on the device.
- A locally drafted deck that the server never saw.
- Local battle results.
- Local card rewards.
- Local card upgrade changes.

This matters because later online systems need to decide whether that local data can be trusted.

Recommended rule:

- Real account progression requires registered server authentication; historical guest data does not bypass the login gate.
- Local offline data should be treated as cache or temporary demo data.
- Local offline rewards, local card ownership changes, and local ticket changes are not merged into real online accounts.
- If true offline progression is added later, it needs a separate explicit merge policy before it can affect server accounts.

## Core Concepts

### Account

An account represents one real user.

The account owns:

- profile data
- linked login identities
- Resource Gold balance
- ticket balance
- card collection
- card upgrade levels
- saved/deployed run decks
- current run state
- reward history

### Auth Identity

An auth identity is one login method attached to an account.

Schema provider identifiers (historical/future entries do not imply a current public login route):

- `game_id`
- `guest` (historical rows only; authentication disabled)
- `naver`
- `kakao`
- `google`

The schema can retain multiple identities for historical data, but current public login supports Game ID and Google only. No guest upgrade/link flow is exposed and provider identities are not automatically merged. Internal link helper code does not create a public API.

Current account rule:

- New Game ID or Google registration creates a registered server account.
- A verified identity resumes its existing account; local cached profile data cannot change that binding.
- Historical guest identities remain disabled and legacy guest credentials are removed by the client.

Identity normalization rule:

- `provider_user_id` stores the original provider id or hashed credential id.
- `normalized_provider_user_id` stores the lookup key used for uniqueness.
- For `game_id`, the normalized id is lower-case and trimmed so login is case-insensitive.
- For `guest` and social providers, preserve the provider id unless that provider's official rules say otherwise.
- Only active identities must be unique by `(provider, normalized_provider_user_id)`.

Game ID policy v1:

- `gameId` is 3-32 characters.
- Allowed characters are English letters, numbers, `_`, and `-`.
- Login is case-insensitive through `normalized_provider_user_id`.
- `password` is 8-128 characters.
- Store only password hashes, never raw passwords.

### Auth Session

An auth session is a temporary API login session for one account.

The current account system uses session and refresh tokens. The guest token entry below identifies obsolete data only:

- `guestToken`: obsolete guest credential; deleted during loading and not accepted for current login.
- `sessionToken`: a short-lived API credential used in `Authorization: Bearer ...`.
- `refreshToken`: a longer-lived device login credential used only to rotate a new token pair.

Rules:

- The server may return raw tokens to Unity when login succeeds.
- The database should store only token hashes.
- Unity stores `sessionToken` and `refreshToken` through the account credential-store boundary and clears obsolete `guestToken` data.
- Android player builds use an Android Keystore AES-GCM key and keep only encrypted payloads in app-private preferences.
- iOS player builds use Keychain generic-password entries with `AfterFirstUnlockThisDeviceOnly` accessibility.
- Unity Editor and the current Windows development build use scoped `PlayerPrefs` only as development storage; this is not the mobile release security model.
- On the first mobile launch after upgrading from an older build, a legacy scoped `PlayerPrefs` credential is copied to secure storage and deleted only after the secure write succeeds.
- A `sessionToken` is valid only while its session row is unrevoked, unexpired and belongs to the account's active login family.
- A `refreshToken` is stored only as a hash in `auth_refresh_tokens` and is rotated on every successful refresh.
- Reusing an already rotated refresh token revokes its token family and requires an explicit login.
- Successful server logout revokes the current session and refresh token. Unity always clears local credentials afterward, including when the request fails, and requires a new login.
- A successful independent Game ID or Google login creates a new active family and revokes older access/refresh credentials without changing account-owned data. The older device receives HTTP 401 / `session_replaced` and the exact message `다른 기기에서 로그인하여 연결이 종료되었습니다.`
- Normal refresh rotates tokens inside the same family and retains its live battle socket. An old device's logout cannot revoke the replacement family. Existing 70-second reconnect and server-authoritative results remain unchanged.
- The client suppresses automatic Google re-login after replacement and preserves pending economy/reward/ad recovery ledgers. See [single-device login](single_device_login.md) for polling, migration, compatibility and rollout limits; source implementation is not live deployment.
- Public identity linking is not part of the current API. Confirmed logout revokes the current device session when the server request succeeds and always clears the local credentials; it does not delete or merge account rows.

### Card Definition

A card definition is the canonical game data for a card.

Examples:

- `Goblin`
- `firebolt`
- `Cerberus`
- `BlueDragon`

For now, the canonical card definition source remains `cards.json`.

The DB references card definitions by stable `card_id`.

### Account Card Progress

Account card progress stores a user's ownership and upgrade state for one card type.

This is one row per account plus card id:

- `account_id`
- `card_id`
- `copy_count`
- `upgrade_level`

This matches the intended Clash Royale-like model:

- Rewards grant extra copies of a card.
- Upgrading that card consumes copies of that card plus currency.
- The card's level increases for that user's account.
- Leftover copies remain after the upgrade.

Current cost and leftover-copy examples (2026-10-01):

- Owning 3 Red Dragon copies and 3 Resource Gold allows Lv.0 -> Lv.1, consuming 3 copies and leaving 0 copies.
- Owning 8 Goblin copies and at least 3 Resource Gold allows Lv.0 -> Lv.1, consuming 3 copies and leaving 5 copies.
- Owning 10 Elf Longbow Scout copies and at least 9 Resource Gold allows two separate upgrades Lv.0 -> Lv.1 -> Lv.2, consuming 3 + 6 copies and leaving 1 copy.

The same target-level table applies to every upgradeable card regardless of rarity: Lv.1/2/3/4/5 cost 3/6/10/20/30 copies and the same Resource Gold, and Lv.6–13 each cost 33/33. [Complete cost table](meta_rules.md#card-upgrade-costs). Individual stat/effect gains remain card-specific.

### Deck

A deck is a list of card ids.

Confirmed run deck rule:

- A drafted deck contains 33 cards.
- A drafted deck is used until the run ends at 33 wins or 3 losses.
- The drafted deck is not a permanent constructed deck by default.

PvE and PvP can share the same deck schema.

### Run

A run is the full-game sequence described by [meta_rules.md](./meta_rules.md):

1. Spend ticket cost to start.
2. Draft one 33-card deck.
3. Fight repeated battles with that deck.
4. End at 33 wins or 3 losses.
5. Receive run-end rewards.

## Entity Relationship Overview

```mermaid
erDiagram
    accounts ||--o{ auth_identities : has
    accounts ||--o{ auth_identity_link_events : records
    auth_identities ||--o{ auth_identity_link_events : audits
    accounts ||--|| user_wallets : owns
    accounts ||--o{ user_card_collection : owns
    accounts ||--o{ decks : saves
    decks ||--o{ deck_cards : contains
    accounts ||--o{ draft_runs : starts
    draft_runs ||--o{ draft_run_picks : records
    draft_runs ||--o| decks : outputs
    accounts ||--o{ pvp_match_queue : queues
    accounts ||--o{ pvp_match_players : plays
    accounts ||--o{ pvp_match_connections : connects
    pvp_matches ||--o{ pvp_match_players : seats
    pvp_matches ||--o{ pvp_match_connections : tracks
    accounts ||--o{ reward_grants : receives
    accounts ||--o{ account_transactions : audits
    accounts ||--o{ user_pack_inventory : owns
    accounts ||--o{ pack_openings : opens
```

## Table Design

### `accounts`

Stores one row per user.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `display_name` | text | Shown in UI |
| `account_kind` | text | `guest`, `registered` |
| `created_at` | timestamp | Server time |
| `updated_at` | timestamp | Server time |
| `last_login_at` | timestamp nullable | Updated on login |
| `is_banned` | boolean | Default false |
| `account_status` | text | `active`, `disabled`, `deleted` |

Notes:

- Do not use display name as identity.
- Do not expose internal account ids unnecessarily to other players.
- A guest account can become registered when it links Game ID or social login.

### `auth_identities`

Stores login providers connected to an account.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `provider` | text | `game_id`, `guest`, `naver`, `kakao`, `google` |
| `provider_user_id` | text | Unique id from provider |
| `normalized_provider_user_id` | text | Canonical lookup id; lower-case for `game_id` |
| `email` | text nullable | If available |
| `password_hash` | text nullable | Only for Game ID password login |
| `password_algorithm` | text nullable | Example: `pbkdf2-sha256`, `argon2id`, or `bcrypt` |
| `password_updated_at` | timestamp nullable | Updated when Game ID password changes |
| `email_verified_at` | timestamp nullable | Set when email verification is confirmed |
| `identity_status` | text | `active`, `revoked` |
| `linked_at` | timestamp | When the identity was linked to the account |
| `revoked_at` | timestamp nullable | When this login method was unlinked/revoked |
| `external_profile` | jsonb | Provider-specific non-authoritative profile payload |
| `updated_at` | timestamp | Server time |
| `created_at` | timestamp | Server time |
| `last_used_at` | timestamp nullable | Updated on login |

Constraints:

- Active identities are unique by `(provider, normalized_provider_user_id)`.
- If password login is used, never store raw passwords.
- Use a modern password hash such as Argon2id or bcrypt.
- `password_hash` and `password_algorithm` are required only for `game_id`.
- Social providers should use provider-subject ids, not display names, as `provider_user_id`.

Notes:

- Guest login should still create a real server-side account row.
- Social login links should attach to the same account, not create duplicates, when the user intentionally links them.
- A revoked identity remains as audit/history, but it must not allow login.

### `auth_identity_link_events`

Stores audit rows for identity lifecycle events.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | Account affected by the event |
| `auth_identity_id` | UUID nullable | Linked identity if available |
| `provider` | text | `game_id`, `guest`, `naver`, `kakao`, `google` |
| `event_type` | text | `created`, `linked`, `unlinked`, `login`, `password_changed` |
| `previous_account_id` | UUID nullable | Used when resolving merge/link conflicts |
| `created_at` | timestamp | Server time |
| `metadata` | jsonb | Debug/audit payload; never store raw passwords/tokens |

Rules:

- Use this table to debug account-linking problems without trusting Unity logs.
- Do not store raw `guestToken`, raw `sessionToken`, raw OAuth access token, or raw password.

### `user_wallets`

Stores account-level currencies.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `account_id` | UUID | Primary key and FK to `accounts.id` |
| `resource_gold` | bigint | Out-of-battle Resource Gold |
| `tickets` | integer | Tickets used to start runs |
| `updated_at` | timestamp | Server time |

Notes:

- Battle gold is not Resource Gold.
- Battle resources should not be stored in this table.
- Currency changes should also create an `account_transactions` audit row.

### `user_card_collection`

Stores what cards a user owns and their upgrade state.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `account_id` | UUID | FK to `accounts.id` |
| `card_id` | text | Stable id from card definition source |
| `copy_count` | integer | Remaining owned copies after upgrades |
| `upgrade_level` | integer | Current upgrade level |
| `created_at` | timestamp | First acquisition time |
| `updated_at` | timestamp | Last change time |

Primary key:

- `(account_id, card_id)`

Rules:

- `copy_count` cannot be negative.
- `upgrade_level` cannot be negative.
- Upgrading consumes copies from the same `card_id`.
- Upgrading also consumes the required currency from `user_wallets`.

### `card_upgrade_costs`

Stores the authoritative card upgrade requirements read by CardUpgradeService when `/cards/upgrade` charges the account. The Unity shared table displays costs and availability; it does not replace the database charge authority.

Migration `0020_card_upgrade_costs_revision.sql` established the 13 Lv.0 -> Lv.1 through Lv.12 -> Lv.13 rows; subsequent `0021_card_upgrade_level2_cost_revision.sql` changes only Lv.1 -> Lv.2 to 6 copies and 6 Resource Gold for the [current cost schedule](meta_rules.md#card-upgrade-costs). Original seed `0003` and migration `0020` are retained unchanged. A live database without the pending revisions keeps its previous costs; source editing does not apply the migration to the live server. Wallets, collection levels, receipts and transaction history are not rewritten. Existing receipts retain their original paid cost when replayed.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `level_from` | integer | Current level before upgrade |
| `level_to` | integer | New level after upgrade |
| `required_copy_count` | integer | Card copies consumed |
| `required_resource_gold` | bigint | Resource Gold consumed |

Primary key:

- `(level_from, level_to)`

Current confirmed policy:

- All upgradeable cards share the target-level cost table; rarity does not change the cost.
- Maximum upgrade level is 13.
- Cost revision does not change upgrade eligibility or per-card stat/effect gains. New cards still require the user's exact eligibility and Lv.1–13 increments under [card authoring rules](card_authoring_checklist.md).

### `decks`

Stores deck headers.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `name` | text | User-facing deck name |
| `deck_type` | text | `draft_run`, `pve`, `pvp`, `constructed` |
| `source_run_id` | UUID nullable | FK to `draft_runs.id` when created by draft |
| `card_definition_version` | text | Version/hash of card data used |
| `created_at` | timestamp | Server time |
| `updated_at` | timestamp | Server time |
| `is_archived` | boolean | Soft hide instead of hard delete |

Notes:

- Draft run decks should be immutable after draft completion.
- PvE and PvP can both point at the same `draft_run` deck.
- Constructed decks can be added later if constructed play is added.

### `deck_cards`

Stores the cards inside a deck.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `deck_id` | UUID | FK to `decks.id` |
| `slot_index` | integer | 0-based card order |
| `card_id` | text | Stable card id |
| `created_at` | timestamp | Server time |

Primary key:

- `(deck_id, slot_index)`

Rules:

- A 33-card draft deck should have exactly 33 rows.
- Store order because UI may want to show draft-pick order.
- Deck validation should also be able to count by `card_id`.
- Do not store card upgrade level in `deck_cards`.

Upgrade level application:

- A run deck stores selected `card_id` values only.
- When a battle starts, the server reads each card's current `upgrade_level` from `user_card_collection`.
- If a card is upgraded while a run is still in progress, future battles in that run use the newer upgrade level.
- Past completed battles are not recalculated.

### `draft_runs`

Stores a run/draft state owned by an account.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `status` | text | `drafting`, `ready`, `in_progress`, `completed`, `abandoned` |
| `mode` | text | `pve`, `pvp` |
| `wins` | integer | Current run wins |
| `losses` | integer | Current run losses |
| `draft_seed` | integer nullable | Optional deterministic draft seed |
| `completed_deck_id` | UUID nullable | FK to `decks.id` |
| `ticket_cost_paid` | integer | Tickets spent to start this run |
| `ended_reason` | text nullable | `wins_33`, `losses_3`, `abandoned` |
| `reward_claimed_at` | timestamp nullable | Server time |
| `started_at` | timestamp | Server time |
| `completed_at` | timestamp nullable | Server time |
| `updated_at` | timestamp | Server time |

Rules:

- Spend tickets once when starting a run.
- A run can exist before the 33-card deck is finished.
- When draft reaches 33 picks, create a `decks` row and set `completed_deck_id`.
- Rewards are granted when the run ends at 33 wins or 3 losses.

### `draft_run_picks`

Stores draft choices and offers for recovery/audit.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `run_id` | UUID | FK to `draft_runs.id` |
| `pick_index` | integer | 0-based pick number |
| `offered_card_ids` | jsonb | The 3 offered cards |
| `selected_card_id` | text | Chosen card id |
| `picked_at` | timestamp | Server time |

Primary key:

- `(run_id, pick_index)`

Why store offers:

- Lets a player resume draft after disconnect.
- Helps debug draft generation.
- Prevents client from inventing a different offered card.

### `pvp_match_queue`

Stores account-backed PvP matchmaking requests.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `run_id` | UUID nullable | Run used for this queue request |
| `deck_id` | UUID nullable | Deck used for this queue request |
| `queue_status` | text | `queued`, `matched`, `canceled`, `expired` |
| `matchmaking_region` | text | Region/shard key, initially `default` |
| `client_version` | text nullable | Used later to reject incompatible clients |
| `matched_match_id` | UUID nullable | FK to `pvp_matches.id` after matching |
| `requested_at` | timestamp | Server time |
| `matched_at` | timestamp nullable | Server time |
| `canceled_at` | timestamp nullable | Server time |
| `expires_at` | timestamp nullable | Server time |
| `updated_at` | timestamp | Server time |
| `metadata` | jsonb | Debug/extension payload |

Rules:

- An account can have only one `queued` request at a time.
- Matchmaking must use authenticated `account_id`, not local player names.
- Canceling matchmaking should update this row instead of deleting it.

### `pvp_match_reservations`

Before a new queue participant connects, `pvp_match_reservations` (migration `0013`, deployed and verified on 2026-09-09) reserves a concrete `PlayerA` or `PlayerB` seat in the same transaction as match selection. Columns are reservation UUID, match UUID, seat, authenticated account UUID, server connection ID, run/deck UUIDs, creation time and expiry. Unique constraints on `(match_id, seat)` and `account_id` prevent duplicate pending seats and pending accounts.

The internal join reservation lasts 60 seconds and is separate from the existing reconnect timer. Same-connection retries reuse it without extending expiry; other simultaneous connections from the same account are rejected. Successful player/connection persistence validates account, connection, seat, run/deck and deadline and consumes the reservation atomically. Failed requests release by reservation UUID plus connection ID; expired abandoned reservations are removed on the next queue resolution. Queue rows retain canceled/expired history. Waiting disconnected seats stop counting as occupied, and empty waiting matches become canceled. Started battles retain the existing reconnect behavior.

Capacity-changing matchmaking transactions share a short PostgreSQL advisory lock in the current single-region service. No network I/O is performed under that lock. In-memory join leases keep the same BattleSession registered while a participant is entering; a battle cannot start before both connections finish DB joining. Validation: `TempBuild/ServerMatchReservations_20260909/README.md`.

### `pvp_matches`

Stores one PvP match lifecycle row.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `match_id` | text | Public/server match identifier used by WebSocket joins |
| `match_status` | text | `waiting`, `active`, `reconnect_grace`, `completed`, `canceled` |
| `matchmaking_region` | text | Region/shard key |
| `client_version` | text nullable | Matched client version |
| `winner_account_id` | UUID nullable | Winning account when completed |
| `ended_reason` | text nullable | `normal`, `forfeit`, `reconnect_timeout`, `server_cancel`, `error` |
| `turn_timeout_seconds` | integer | Current default: 73 |
| `reconnect_grace_seconds` | integer | Current runtime default: 70; historical rows keep their recorded value |
| `created_at` | timestamp | Server time |
| `started_at` | timestamp nullable | Server time |
| `completed_at` | timestamp nullable | Server time |
| `updated_at` | timestamp | Server time |
| `metadata` | jsonb | Debug/extension payload |

Rules:

- `match_id` must be unique.
- Only the server can set the winner.
- A completed match can still be queried so clients can show the final result.

### `pvp_match_players`

Stores which authenticated account owns each seat in a PvP match.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `match_id` | UUID | FK to `pvp_matches.id` |
| `seat` | text | `PlayerA`, `PlayerB` |
| `account_id` | UUID | FK to `accounts.id` |
| `run_id` | UUID nullable | Run associated with the battle |
| `deck_id` | UUID nullable | Deck associated with the battle |
| `runtime_player_id` | text | Temporary battle-model seat: `Player` or `AI` |
| `player_status` | text | `active`, `disconnected`, `forfeited`, `winner`, `loser` |
| `joined_at` | timestamp | Server time |
| `last_seen_at` | timestamp nullable | Server time |
| `disconnected_at` | timestamp nullable | Server time |
| `reconnect_deadline_at` | timestamp nullable | Server time |
| `final_result` | text nullable | `win`, `loss`, `draw` |
| `session_version` | integer | Incrementable reconnect/session generation |
| `metadata` | jsonb | Debug/extension payload |

Rules:

- `(match_id, seat)` is unique.
- `(match_id, account_id)` is unique.
- Reconnect uses account identity to recover the same `match_id` and `seat`.
- `runtime_player_id` exists only because the current shared battle model still uses `Player` and `AI` internally.

### `pvp_match_connections`

Stores connection history for a PvP match seat.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `match_id` | UUID | FK to `pvp_matches.id` |
| `seat` | text | `PlayerA`, `PlayerB` |
| `account_id` | UUID | FK to `accounts.id` |
| `connection_id` | text | Server-generated connection id |
| `connection_status` | text | `active`, `closed`, `replaced`, `rejected` |
| `connected_at` | timestamp | Server time |
| `last_seen_at` | timestamp nullable | Server time |
| `disconnected_at` | timestamp nullable | Server time |
| `remote_endpoint` | text nullable | Debug only |
| `user_agent` | text nullable | Debug only |
| `metadata` | jsonb | Debug/extension payload |

Rules:

- Only one active connection per account per match is allowed.
- A reconnect should close or replace the previous connection row, then create a new active row.
- Do not store secrets in connection metadata.

### `reward_grants`

Stores reward payouts.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `source_type` | text | `run_end`, `admin`, `event`, `quest`, etc. |
| `source_id` | UUID nullable | Example: `draft_runs.id` |
| `resource_gold_delta` | bigint | Rewarded Resource Gold |
| `ticket_delta` | integer | Rewarded tickets |
| `pack_rewards` | jsonb | Pack ids/counts |
| `card_rewards` | jsonb | Direct eligible card ids/counts for current run-end rewards |
| `created_at` | timestamp | Server time |

Notes:

- Current temporary run-end formula is defined in [meta_rules.md](meta_rules.md); final balance can still change only by a confirmed request.
- Normal-run rewards are claimed once at run end. Per-run Gold Dungeon, ads and gifts have separate settlement rules.
- Packs can be granted here, then opened later through pack-opening logic.

The following unopened-inventory and pack-opening-history tables are retained as future design examples. The current fixed card pack uses wallet/collection transactions and account operation receipts; clicking to open does not debit inventory or grant cards.

### `user_pack_inventory`

Stores unopened packs owned by the user.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `account_id` | UUID | FK to `accounts.id` |
| `pack_id` | text | Stable pack definition id |
| `quantity` | integer | Unopened pack count |
| `updated_at` | timestamp | Server time |

Primary key:

- `(account_id, pack_id)`

Notes:

- Pack definitions can stay in config until pack rules are fixed.
- Opening a pack should reduce quantity and create card-copy rewards.

### `pack_openings`

Stores pack-opening history.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `pack_id` | text | Opened pack id |
| `granted_cards` | jsonb | Card ids/counts granted |
| `source_reward_grant_id` | UUID nullable | FK to `reward_grants.id` |
| `created_at` | timestamp | Server time |

Why this matters:

- Helps debug missing cards.
- Lets the UI show pack-opening history later.
- Makes reward disputes easier to inspect.

### `account_transactions`

Stores an audit log for account economy changes.

Suggested fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID | Primary key |
| `account_id` | UUID | FK to `accounts.id` |
| `transaction_type` | text | `reward`, `pack_open`, `purchase`, `craft`, `upgrade`, `ticket_spend`, `admin_adjustment` |
| `resource_gold_delta` | bigint | Positive or negative |
| `ticket_delta` | integer | Positive or negative |
| `pack_id` | text nullable | When a pack is affected |
| `pack_count_delta` | integer nullable | Positive or negative |
| `card_id` | text nullable | When a card is affected |
| `card_count_delta` | integer nullable | Positive or negative |
| `upgrade_level_before` | integer nullable | For upgrade audit |
| `upgrade_level_after` | integer nullable | For upgrade audit |
| `source_table` | text nullable | Example: `reward_grants` |
| `source_id` | UUID nullable | Linked source row |
| `created_at` | timestamp | Server time |

Why this matters:

- Helps debug missing rewards.
- Helps recover from bugs.
- Makes future admin tools safer.

## Important Server Rules

### Server Authority

The client must not be trusted for:

- account id
- owned card count
- card upgrade level
- Resource Gold
- ticket count
- reward amount
- pack contents
- card upgrade result
- deck legality
- run win/loss result
- battle result

The server must validate all persistence changes.

### Card Definition Versioning

Saved decks should store a card definition version or hash.

Reason:

- If `cards.json` changes, old saved decks may need migration or validation.
- This helps explain why an old deck became invalid after a balance patch.

Suggested first version:

- Use a manually maintained `card_definition_version` string.

Future version:

- Hash the loaded card database and store that hash.

### Deck Validation

When saving or starting a deck, validate:

- card ids exist in the server card definition source
- deck size is correct for its type
- copy limits are respected
- account owns required cards, if the mode requires ownership
- draft decks match recorded draft picks

### Draft Resume

Current implementation:

- The server creates a `draft_runs` row before the first offer.
- The server loads the canonical card database and generates the opening and subsequent offers.
- Offer generation is deterministic from `draft_runs.draft_seed` plus the ordered server pick history, so it can be reproduced after reconnect or process restart.
- Unity sends only `runId`, the selected `cardId`, and the expected zero-based `pickIndex`.
- Before accepting a pick, the server reconstructs the current offer and validates run ownership, pick order, rarity, offer membership, copy limits, and deck capacity.
- The server persists the accepted pick's actual offer in `draft_run_picks.offered_card_ids`, stores the next offer in `draft_runs.current_offer_card_ids`, and commits both atomically.
- Re-login restores the same ordered picks and the exact saved current offer.
- The 33rd accepted selection creates the locked `decks` and `deck_cards` records in the same transaction; Unity cannot submit an arbitrary completed deck.
- Stale/replayed pick indexes are rejected with `409 Conflict`.

### Run End And Rewards

Only server-resolved battle results advance the persisted run counters. The reward service reads these counters from the database; client-provided win/loss totals and offline battle results are never accepted. Clients awaiting persistence refresh /me instead of uploading local totals. The retired local-result endpoint is rejected before request deserialization, authentication, or database access in every environment.

When a run reaches 33 wins or 3 losses:

- Set `draft_runs.status` to `completed`.
- Set `draft_runs.ended_reason`.
- Set `draft_runs.completed_at`.
- Wait for the authenticated account to claim the completed run reward.
- On a successful one-time claim, create a `reward_grants` row.
- Add Resource Gold and eligible card copies to the account.
- Create `account_transactions` rows for the economy changes.

## Implemented API Direction

Account and meta operations use authenticated HTTP endpoints. Card-pack POST/receipt GET require bearer authentication and share the account API IP limit (default 120 per 60 seconds). Live battle commands and state use the `/battle` WebSocket.

Implemented account/meta endpoints:

| Endpoint | Purpose |
| --- | --- |
| `POST /auth/register` | Create Game ID account |
| `POST /auth/login` | Login with Game ID |
| `POST /auth/google` | Validate Google identity and log in/create registered account |
| `POST /auth/google/desktop-token` | Exchange Desktop authorization code for immediate Google login |
| `POST /auth/refresh` | Rotate saved device credentials |
| `POST /auth/logout` | Revoke the current login family |
| `GET /auth/session-status` | Lightweight current-login validation; replaced credentials return 401 / session_replaced |
| `GET /me` | Current profile |
| `POST /wallet/purchase-ticket` | Buy tickets with Resource Gold |
| `POST /shop/purchase-card-pack` | Purchase the fixed 33 Resource Gold pack with RequestId; atomically grant 10 cards |
| `GET /shop/card-pack-purchases/{requestId}` | Read this account's original purchase result without charging or drawing again |
| `POST /cards/upgrade` | Upgrade an eligible card using RequestId and ExpectedUpgradeLevel |
| `POST /account/operations/resolve` | Complete/cancel a saved ticket/card-pack purchase or upgrade request without charging again |
| `GET /gifts`, `POST /gifts/{giftId}/claim` | List/claim this account's pending administrator gifts |
| `POST /ads/rewarded-ticket/attempt`, `GET /ads/rewarded-ticket/attempt/{attemptId}` | Start/query opt-in server-verified ticket rewards |
| `POST /runs/start` | Spend tickets and start a persisted draft run |
| `POST /runs/draft-state` | Read the authoritative selected picks and current offer |
| `POST /runs/select-draft-card` | Select one card from the authoritative offer and advance the draft |
| `POST /runs/save-draft-picks` | Removed client-authoritative compatibility route; returns `410 Gone` |
| `POST /runs/complete-draft` | Removed client-authoritative compatibility route; returns `410 Gone` |
| `POST /runs/sync-local-record` | Removed client-authoritative result route; always returns 410 Gone with client_authoritative_run_result_removed |
| `POST /runs/claim-rewards` | Claim one completed run reward |
| `GET /battle/reconnect-status` | Discover the original human/AI battle, seat, mode and server remaining time |
| `GET /pvp/reconnect-status` | Legacy PvP-only compatibility query |
| `GET /ai-challenge/status`, `POST /ai-challenge/start`, `GET /ai-challenge/attempts/{id}` | Three admissions per paid active run, run-scoped status and independent result receipts |

PvP battle endpoints should still use server-authoritative command messages.

## Implementation Status

### Implemented

- Source migrations `0001` through `0025` for account/meta data, rotating sessions, rewards/ads, guest disabling, draft and matchmaking atomicity, battle receipts, operation resolution, gifts, battle modes, revised upgrade costs, card-pack receipts, run-scoped challenge usage, ElfElder ID normalization and single-login families; production last confirmed through `0024`
- Opaque session-token authentication for registered accounts; guest creation/resume rejected
- Game ID registration/login using stable server account identity
- Google ID-token verification and Google login/account creation; provider identities are not automatically merged
- Unity Google-auth HTTP contracts and account-session response application
- Windows Desktop OAuth system-browser flow with loopback callback, `state`, PKCE, nonce validation, and start-scene login button
- Android Credential Manager Google login with an explicit account-selection flow, nonce validation, and the same server login contract
- First-launch account login gate plus automatic saved-session restore and refresh-token rotation on later launches
- Authenticated `/auth/refresh` replacement-token flow and `/auth/logout` device-session revocation
- Platform credential-store boundary with legacy migration, Android Keystore-backed encrypted storage, and iOS Keychain storage
- Authenticated `/me` profile, wallet, collection, latest run, and reconnect data
- Persistent Resource Gold, tickets, card copies, upgrade levels, and upgrade audit transactions
- Ticket purchase and ticket spending when a new run starts
- Card-pack purchase with atomic 33 Resource Gold debit/10-card collection grant, immutable ordered receipts, cancellation protection, saved-request recovery and local opening presentation
- Server-authoritative draft offers, persistent picks, exact resume, atomic completed 33-card deck creation, run wins/losses, and run resume
- One-time run-end Resource Gold and random card-copy rewards
- Account-backed normal matchmaking with server AI fallback, seat ownership, 70-second running-server reconnect, result receipts/outbox and command logs
- Per-run Gold Dungeon admissions/refunds and independent gold settlement, purchase/upgrade resolution, administrator gifts and S2S-verified ad ticket rewards
- Mandatory cancellation of unfinished battles after server interruption; no win/loss, mode-specific challenge refund

### Partially Implemented

- These registrations were pending at the original design checkpoint. They were configured and transferred to Lightsail; Android production login and Windows Editor Play Google login were later user-confirmed. The current operator guide describes the remaining platform-specific verification scope.
  - Android requires package `com.after333.game`, the signing-certificate SHA, and the Web application client ID as its server audience.
  - Android/iOS secure credential storage is implemented, but each platform still needs a real-device build smoke test before release.
- Identity schema supports `kakao` and `naver`, but their production token verification and Unity provider login flows are not implemented.
- Pack inventory/history tables remain placeholders for a future inventory-based design; the fixed card-pack purchase grants directly and its opening presentation is implemented without consuming those tables.
- Production load and end-to-end device/network validation remain separate from source/isolated test records. Startup snapshot revival is superseded by the cancellation policy; `StartupRecoveryEnabled=false` is expected.

### Deferred

- Production Kakao and Naver OAuth login/linking
- Individual-card shop inventory, additional pack products, server-owned unopened inventory/history UX, and crafting beyond the fixed card-pack and ticket purchases
- Paid purchases
- Ranked matchmaking, MMR, seasons, and ladders
- Production abuse protection beyond current server-side battle validation

## Open Issues / TBD

These rules are intentionally not decided by this document:

- final reward balance beyond the current temporary Resource Gold/card-copy formula
- additional pack products and guarantees beyond the confirmed 33 Resource Gold/10 independent cards/40·30·18·9·3% probabilities
- additional rewards beyond the confirmed Gold Dungeon, ad ticket and administrator gift flows
- individual-card shop inventory and pricing beyond the confirmed ticket/card-pack products
- crafting rules
- ticket acquisition routes beyond the current Resource Gold purchase, verified ads and administrator gifts
- exact non-credential profile-cache retention behavior
- whether true offline progression will ever be added as a separate mode
- production OAuth provider setup and account-recovery policy beyond the locked Google no-auto-merge rule

## Next Implementation Recommendation

### Connection stability follow-up (2026-09-09; source verified and deployed)

- WebSocket send errors, closed transports, receive termination and heartbeat expiry converge on one disconnect cleanup. The session reserves the original seat and detaches the socket under one lock; repeated cleanup is a no-op.
- Start the existing grace expiry timer before DB/network awaits. A DB failure cannot prevent the in-memory grace timer from being scheduled.
- Reconnect joins and disconnect persistence serialize on the same match row. Only closing the exact active connection may mark its seat disconnected; late cleanup of a replaced socket cannot overwrite a newer join or extend a previous deadline.
- Envelope sends and close-output responses share a per-connection semaphore. State views are created inside the gate; the five-second send deadline includes gate wait. A sender's HTTP cancellation does not propagate to the opponent's broadcast send.
- This change requires no new DB migration. It does not introduce global event sequencing, a bounded send queue, new matchmaking rules, or additional server-restart recovery guarantees.
- See `TempBuild/ServerConnectionStability_20260909/README.md` for isolated validation. The same verified binary was deployed at 20:02 KST; see `TempBuild/ServerConnectionStabilityDeploy_20260909/README.md`. Public HTTP/WSS checks passed; Unity visual reconnect validation remains blocked by the Computer Use capture error.

### Other setup recommendations

1. Create the Google Cloud Desktop, Web application, and Android OAuth clients described in `docs/google_auth_setup.md`.
2. Run live Windows Google login and persistence smoke tests through HTTPS.
3. Run Android Google login, automatic session restore and secure credential migration/logout smoke tests on a real device.
4. Deploy the published server and PostgreSQL to a staging host behind HTTPS/WSS when a VPS is introduced.
5. Add Kakao and Naver only after both Google platform flows are stable.

### Draft-run start atomicity (migration 0014; deployed and verified 2026-09-09)

`draft_runs_one_active_per_account_idx` is a unique partial index on `draft_runs(account_id)` with predicate `status in ('drafting', 'ready', 'in_progress')`. It applies across modes and to status transitions as well as inserts. Legacy duplicates are not repaired automatically; index creation and the migration version insert roll back together on failure.

The server acquires `user_wallets ... FOR UPDATE` for the authenticated account before checking all active runs and unclaimed completed runs, under READ COMMITTED isolation. Competing starts wait before the read and see the winning transaction's committed run. Wallet debit, run, initial offer and ticket-spend ledger entry commit or roll back as a unit. The lookup does not lock an existing run while holding the wallet, avoiding reversed run/wallet lock order with reward claims. Different accounts remain independent.

Completed historical rows are not constrained by the active-run index. Their unclaimed rewards are checked by the service and must not be silently discarded during deployment. HTTP start retries against an active run retain the existing 409 `active_run_exists` contract rather than creating a new run or returning an idempotency receipt.
## 골드 던전 상태 계약 (2026-10-09)

`GET /ai-challenge/status`는 `RunId`, `RunLimit=3`, `RemainingAttempts`, `PendingAttempt`를 반환하고 다음 일일 초기화 시각을 반환하지 않는다. 인증 계정의 완성된 진행 중 런에서 `(account_id, run_id)`별 비환급 도전을 합산하여 `max(0, 3 - 사용 횟수)`를 계산한다. 기존 런의 과거 날짜 도전도 포함한다. 진행 중 완성된 런이 없으면 입장은 불가능하고 잔여 횟수는 0이다.

원래 런에 귀속된 취소만 해당 소비를 멱등 환급한다. 새 런의 횟수와 과거 런의 환급은 섞지 않으며, 만료 패배·정상 승패·앱 종료는 환급 사유가 아니다. 새 런 시작에는 기존 티켓 3개 비용을 지불하고, 던전 입장에는 추가 티켓을 부과하지 않는다.

운영 DB의 총22개/latest0022 적용 기록은 2026-10-02의 당시 사실이다. 이번 소스의 0023 존재는 운영 적용·서버 배포·클라이언트 빌드 완료를 뜻하지 않는다.

## ElfElder 카드 ID 통일 (2026-10-09 소스 변경)

마이그레이션 `0024_elf_elder_card_id.sql`은 기존 카드 식별자를 공백 없는 `ElfElder`로 변환한다. 보유 카드 두 철자가 함께 있으면 잔여 수량을 합산하고 높은 강화 레벨을 유지한다. 덱 슬롯·드래프트 제시/선택·보상·카드팩·전투 JSON·거래 기록·성공 영수증 및 강화 요청 fingerprint를 같은 트랜잭션에서 변환하며 골드·티켓·수량 증감·요청 ID·결과를 바꾸지 않는다. JSON 오브젝트에 두 ID 키가 함께 있으면 임의 병합하지 않고 전체 롤백한 뒤 확인한다. 서버 업데이트 시 이전 실행본의 쓰기를 중지하고 새 클라이언트와 함께 적용해야 한다. 운영 DB는 여전히 0023이며 이번에는 0024를 운영에 적용하지 않았다.
