# PostgreSQL DB Implementation Plan

> **Current authentication policy (2026-07-16):** Guest account creation, resume, and identity linking are disabled. Any guest-auth sections below are retained only as historical design context and must not be implemented or exposed. Current clients require Game ID or Google login, restore saved session/refresh credentials automatically, and otherwise show a non-dismissible login gate.

## Document Purpose

This document is the implementation bridge between the high-level account DB design and the actual PostgreSQL-backed server code.

Related documents:

- Account persistence design: [account_persistence_db_design.md](./account_persistence_db_design.md)
- Meta rules: [meta_rules.md](./meta_rules.md)
- Draft rules: [draft_rules.md](./draft_rules.md)
- Online PvP prototype: [online_pvp_prototype.md](./online_pvp_prototype.md)

## Confirmed Direction

- The real source of truth is a server DB.
- The target DB is PostgreSQL.
- Unity must not write authoritative account, ticket, card, reward, or run data directly.
- Guest accounts are real server-side accounts.
- Local Unity data is only cache or temporary demo data.
- Run decks store `card_id` values only.
- Each battle reads the account's current card upgrade levels from `user_card_collection`.

## Implementation Goal

The first DB implementation should not try to build every future meta feature at once.

The first useful vertical slice is:

1. Server can connect to PostgreSQL.
2. Server can create a guest account.
3. Server can create a Game ID account.
4. Server can return `GET /me`.
5. Server can return wallet/ticket/card summary.
6. Server can save and reload a completed 33-card draft deck.

Rewards, packs, shop, crafting, social login, and full card upgrade formulas should remain later phases.

## Recommended Server Stack

Recommended for the C# server:

- PostgreSQL
- Npgsql PostgreSQL driver
- EF Core migrations or SQL migration scripts

Preferred simple path:

- Use SQL migration scripts first.
- Add a small DB access layer in the server.
- Avoid letting battle logic directly know SQL details.

Possible later path:

- Move to EF Core migrations if model-based migration management becomes useful.

## Server Layering

Recommended server-side structure:

```text
Project333.PvpServer
  Persistence/
    Db/
      DbConnectionFactory
      DbMigrator
    Accounts/
      AccountRepository
      AuthIdentityRepository
      WalletRepository
    Collections/
      CardCollectionRepository
    Decks/
      DeckRepository
    Runs/
      DraftRunRepository
```

Rule:

- WebSocket battle session code should not run raw SQL.
- Account/run/deck persistence should go through repositories or services.
- Battle state can still remain in memory until resumable battles are intentionally implemented.

## Configuration

Recommended environment variables:

| Name | Purpose | Example |
| --- | --- | --- |
| `PROJECT333_DB_CONNECTION` | PostgreSQL connection string | `Host=localhost;Port=5432;Database=project333;Username=project333;Password=dev_password` |
| `PROJECT333_JWT_SECRET` | Future auth token signing secret | Development-only random value |
| `PROJECT333_CARD_DATA_VERSION` | Current card definition version | `cards-v1` |

Development rule:

- Do not hardcode real database passwords in source files.
- Local development may use a documented dummy password.

## Migration Strategy

Recommended migration folder:

```text
Server/Project333.PvpServer/Persistence/Migrations/
  0001_account_meta_schema.sql
  0002_auth_sessions.sql
    0003_card_upgrade_costs_seed.sql
    0004_draft_current_offer.sql
    0005_auth_identity_login_expansion.sql
    0006_pvp_matchmaking_schema.sql
  ```

Recommended migration tracking table:

- `schema_migrations`

Each migration script should insert its version into `schema_migrations` after successful execution.

## Migration 0001 Draft

This is a draft only. Do not run it automatically until the server migration runner is implemented.

```sql
begin;

create extension if not exists pgcrypto;

create table if not exists schema_migrations (
    version text primary key,
    applied_at timestamptz not null default now()
);

create table accounts (
    id uuid primary key default gen_random_uuid(),
    display_name text not null,
    account_kind text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    last_login_at timestamptz null,
    is_banned boolean not null default false,
    account_status text not null default 'active',
    constraint accounts_account_kind_check check (account_kind in ('guest', 'registered')),
    constraint accounts_account_status_check check (account_status in ('active', 'disabled', 'deleted'))
);

create table auth_identities (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    provider text not null,
    provider_user_id text not null,
    email text null,
    password_hash text null,
    created_at timestamptz not null default now(),
    last_used_at timestamptz null,
    constraint auth_identities_provider_check check (provider in ('game_id', 'guest', 'naver', 'kakao', 'google')),
    constraint auth_identities_provider_user_unique unique (provider, provider_user_id)
);

create index auth_identities_account_id_idx on auth_identities(account_id);

create table user_wallets (
    account_id uuid primary key references accounts(id) on delete cascade,
    resource_gold bigint not null default 0,
    tickets integer not null default 0,
    updated_at timestamptz not null default now(),
    constraint user_wallets_resource_gold_nonnegative check (resource_gold >= 0),
    constraint user_wallets_tickets_nonnegative check (tickets >= 0)
);

create table user_card_collection (
    account_id uuid not null references accounts(id) on delete cascade,
    card_id text not null,
    copy_count integer not null default 0,
    upgrade_level integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (account_id, card_id),
    constraint user_card_collection_copy_count_nonnegative check (copy_count >= 0),
    constraint user_card_collection_upgrade_level_nonnegative check (upgrade_level >= 0)
);

create index user_card_collection_card_id_idx on user_card_collection(card_id);

create table card_upgrade_costs (
    level_from integer not null,
    level_to integer not null,
    required_copy_count integer not null,
    required_resource_gold bigint not null,
    primary key (level_from, level_to),
    constraint card_upgrade_costs_level_step_check check (level_to = level_from + 1),
    constraint card_upgrade_costs_level_from_nonnegative check (level_from >= 0),
    constraint card_upgrade_costs_required_copy_count_nonnegative check (required_copy_count >= 0),
    constraint card_upgrade_costs_required_resource_gold_nonnegative check (required_resource_gold >= 0)
);

create table draft_runs (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    status text not null,
    mode text not null,
    wins integer not null default 0,
    losses integer not null default 0,
    draft_seed integer null,
    completed_deck_id uuid null,
    ticket_cost_paid integer not null default 0,
    ended_reason text null,
    reward_claimed_at timestamptz null,
    started_at timestamptz not null default now(),
    completed_at timestamptz null,
    updated_at timestamptz not null default now(),
    constraint draft_runs_status_check check (status in ('drafting', 'ready', 'in_progress', 'completed', 'abandoned')),
    constraint draft_runs_mode_check check (mode in ('pve', 'pvp')),
    constraint draft_runs_wins_range check (wins >= 0 and wins <= 33),
    constraint draft_runs_losses_range check (losses >= 0 and losses <= 3),
    constraint draft_runs_ticket_cost_paid_nonnegative check (ticket_cost_paid >= 0),
    constraint draft_runs_ended_reason_check check (ended_reason is null or ended_reason in ('wins_33', 'losses_3', 'abandoned'))
);

create index draft_runs_account_id_idx on draft_runs(account_id);
create index draft_runs_status_idx on draft_runs(status);

create table decks (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    name text not null,
    deck_type text not null,
    source_run_id uuid null references draft_runs(id) on delete set null,
    card_definition_version text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    is_archived boolean not null default false,
    constraint decks_deck_type_check check (deck_type in ('draft_run', 'pve', 'pvp', 'constructed'))
);

create index decks_account_id_idx on decks(account_id);
create index decks_source_run_id_idx on decks(source_run_id);

alter table draft_runs
    add constraint draft_runs_completed_deck_id_fk
    foreign key (completed_deck_id) references decks(id) on delete set null;

create table deck_cards (
    deck_id uuid not null references decks(id) on delete cascade,
    slot_index integer not null,
    card_id text not null,
    created_at timestamptz not null default now(),
    primary key (deck_id, slot_index),
    constraint deck_cards_slot_index_nonnegative check (slot_index >= 0)
);

create index deck_cards_card_id_idx on deck_cards(card_id);

create table draft_run_picks (
    run_id uuid not null references draft_runs(id) on delete cascade,
    pick_index integer not null,
    offered_card_ids jsonb not null,
    selected_card_id text not null,
    picked_at timestamptz not null default now(),
    primary key (run_id, pick_index),
    constraint draft_run_picks_pick_index_nonnegative check (pick_index >= 0)
);

create table reward_grants (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    source_type text not null,
    source_id uuid null,
    resource_gold_delta bigint not null default 0,
    ticket_delta integer not null default 0,
    pack_rewards jsonb not null default '{}'::jsonb,
    card_rewards jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create index reward_grants_account_id_idx on reward_grants(account_id);
create index reward_grants_source_idx on reward_grants(source_type, source_id);

create table user_pack_inventory (
    account_id uuid not null references accounts(id) on delete cascade,
    pack_id text not null,
    quantity integer not null default 0,
    updated_at timestamptz not null default now(),
    primary key (account_id, pack_id),
    constraint user_pack_inventory_quantity_nonnegative check (quantity >= 0)
);

create table pack_openings (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    pack_id text not null,
    granted_cards jsonb not null,
    source_reward_grant_id uuid null references reward_grants(id) on delete set null,
    created_at timestamptz not null default now()
);

create index pack_openings_account_id_idx on pack_openings(account_id);

create table account_transactions (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    transaction_type text not null,
    resource_gold_delta bigint not null default 0,
    ticket_delta integer not null default 0,
    pack_id text null,
    pack_count_delta integer null,
    card_id text null,
    card_count_delta integer null,
    upgrade_level_before integer null,
    upgrade_level_after integer null,
    source_table text null,
    source_id uuid null,
    created_at timestamptz not null default now(),
    constraint account_transactions_type_check check (
        transaction_type in ('reward', 'pack_open', 'purchase', 'craft', 'upgrade', 'ticket_spend', 'admin_adjustment')
    )
);

create index account_transactions_account_id_idx on account_transactions(account_id);
create index account_transactions_source_idx on account_transactions(source_table, source_id);
create index account_transactions_card_id_idx on account_transactions(card_id);

insert into schema_migrations(version) values ('0001_account_meta_schema');

commit;
```

## Migration 0002 Auth Sessions

`0002_auth_sessions.sql` adds the server-side session table used by guest login, Game ID login, and future social login.

Design rule:

- The server returns raw `sessionToken` values to the Unity client only once.
- The database stores only `session_token_hash`, never the raw token.
- A session is valid only when `revoked_at is null` and `expires_at > now()`.
- `guestToken` is a long-lived resume credential for guest identity lookup.
- `sessionToken` is the short-lived request credential used by APIs such as `GET /me`.

```sql
begin;

create table if not exists auth_sessions (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    session_token_hash text not null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    last_used_at timestamptz null,
    user_agent text null,
    client_version text null,
    ip_address inet null,
    constraint auth_sessions_token_hash_unique unique (session_token_hash),
    constraint auth_sessions_expires_after_created check (expires_at > created_at),
    constraint auth_sessions_revoked_after_created check (revoked_at is null or revoked_at >= created_at)
);

create index if not exists auth_sessions_account_id_idx on auth_sessions(account_id);
create index if not exists auth_sessions_active_lookup_idx
    on auth_sessions(session_token_hash, expires_at)
    where revoked_at is null;
create index if not exists auth_sessions_account_active_idx
    on auth_sessions(account_id, expires_at)
    where revoked_at is null;

insert into schema_migrations(version) values ('0002_auth_sessions');

commit;
```

## Migration 0005 Auth Identity Login Expansion

`0005_auth_identity_login_expansion.sql` expands the original `auth_identities` table so it can support guest login, first-party Game ID login, future Google/Kakao/Naver social login, and account-link audit history.

Purpose:

- Keep all login methods attached to one `accounts.id`.
- Allow guest accounts to later link Game ID or social login without losing wallet, cards, deck, run, or rewards.
- Store Game ID password metadata without storing raw passwords.
- Store social provider profile hints without trusting them as authoritative progression data.
- Keep audit events for identity creation, login, linking, unlinking, and password changes.

Important columns added to `auth_identities`:

| Column | Purpose |
| --- | --- |
| `normalized_provider_user_id` | Canonical lookup key; lower-case for `game_id`, provider-preserved for guest/social ids |
| `password_algorithm` | Password hash algorithm marker for Game ID login |
| `password_updated_at` | Last password update time |
| `email_verified_at` | Optional email verification marker |
| `identity_status` | `active` or `revoked` |
| `linked_at` | When the login method was linked |
| `revoked_at` | When the login method was revoked/unlinked |
| `external_profile` | Provider-specific profile payload; never authoritative progression data |
| `updated_at` | Last identity-row update time |

New table:

- `auth_identity_link_events`

Rules:

- Active identities are unique by `(provider, normalized_provider_user_id)`.
- `game_id` identities require `password_hash` and `password_algorithm`.
- Non-`game_id` identities must not store `password_hash`.
- Raw passwords, raw session tokens, raw guest tokens, and raw OAuth access tokens must never be stored.
- `auth_identity_link_events.metadata` is for debugging/audit only and must not contain secrets.

## Migration 0006 PvP Matchmaking Schema

`0006_pvp_matchmaking_schema.sql` adds the DB foundation for account-backed PvP matchmaking and reconnect.

Purpose:

- Store one queued PvP request per account.
- Store created PvP matches with server-side lifecycle status.
- Store which authenticated account owns each match seat.
- Store connection rows so reconnect can attach the same account back to the same seat.

Tables:

- `pvp_match_queue`
- `pvp_matches`
- `pvp_match_players`
- `pvp_match_connections`

Important notes:

- `PvpMatchmakingService` now writes and reads these rows for queue joins, match creation, seat ownership, connections, reconnect grace, and match completion.
- The live `BattleSession` remains in memory while a match is active, but account and match ownership are persisted.

## Migration 0007 PvP Battle Snapshots

`0007_pvp_battle_snapshots.sql` adds restorable authoritative battle state and command history for reconnect and optional server-startup recovery.

Tables:

- `pvp_battle_snapshots`
- `pvp_battle_command_log`

Current behavior:

- Save a restorable domain-state snapshot after battle start and accepted state-changing actions.
- Record accepted and rejected client commands with sender identity and produced events when available.
- Save server AI actions, turn-timeout actions, reconnect joins, and disconnect-forfeit resolution.
- Restore a missing in-memory `BattleSession` from the latest valid snapshot when an authenticated reconnect is allowed.
- Keep startup recovery configuration-controlled so local development can leave it disabled.

## Migration 0008 Auth Refresh Tokens

`0008_auth_refresh_tokens.sql` adds rotating, long-lived device login credentials without changing legacy `auth_sessions` rows.

Design:

- `sessionToken` remains the short-lived bearer credential for `/me`, run, collection, and battle APIs.
- `refreshToken` is accepted only by `POST /auth/refresh` and is stored in PostgreSQL only as `refresh_token_hash`.
- Every successful refresh creates a new `auth_sessions` row and a new `auth_refresh_tokens` row.
- The previous session and refresh token are revoked atomically in the same transaction.
- Refresh-token reuse revokes the complete `token_family_id`, forcing an explicit login.
- `POST /auth/logout` revokes the current device session and refresh token.
- Default lifetimes are controlled by `PROJECT333_SESSION_TOKEN_DAYS=7` and `PROJECT333_REFRESH_TOKEN_DAYS=30`.

Unity startup order:

1. Load locally saved credentials.
2. Try the current `sessionToken` with `/me`.
3. If it is invalid and a `refreshToken` exists, rotate it through `/auth/refresh`.
4. Restore `/me` using the replacement session token.
5. Show the login gate only when no reusable credential remains.

## Important Migration Notes

### Card Id Foreign Keys

The migration does not add a SQL foreign key for `card_id`.

Reason:

- Static card definitions currently live in `cards.json`, not a PostgreSQL `cards` table.
- The server must validate `card_id` values against the loaded card definition provider.

Future option:

- Add a `card_definitions` table if the server later owns card data directly.

### Deck Card Count

The database cannot easily enforce "exactly 33 cards" with a simple row constraint.

The server should validate:

- draft run deck has exactly 33 `deck_cards`
- per-card copy limits are respected
- offered draft cards match `draft_run_picks`

### Current Upgrade Levels

Do not snapshot upgrade levels into `deck_cards`.

At battle start:

1. Load deck `card_id` list.
2. Load current `upgrade_level` for each card from `user_card_collection`.
3. Build battle deck using the current levels.

### Auth Token Storage

Do not store raw login tokens in PostgreSQL.

Recommended token handling:

- Generate `guestToken` and `sessionToken` using a cryptographically strong random generator.
- Store `guestToken` as a stable guest identity value only after hashing or otherwise treating it as a secret.
- Store `sessionToken` only as `session_token_hash` in `auth_sessions`.
- For `game_id`, store only the password hash and algorithm marker.
- For social providers, store only the stable provider subject id as `provider_user_id`; do not store provider access tokens in `auth_identities`.
- Compare incoming bearer tokens by hashing the incoming token and querying the hash.
- Revoke sessions by setting `revoked_at`.
- Refresh or replace sessions by creating a new `auth_sessions` row.

## First API Slice

### `POST /auth/guest`

Purpose:

- Create or resume a server-side guest account.
- Create a fresh API session for that account.

Suggested request:

```json
{
  "guestToken": "optional-existing-token",
  "clientVersion": "dev-build"
}
```

### `POST /auth/register`

Purpose:

- Create a new first-party Game ID account.
- Create a fresh API session for that account.

Temporary Game ID policy v1:

- `gameId`: 3-32 characters.
- Allowed `gameId` characters: English letters, numbers, `_`, `-`.
- `gameId` login is case-insensitive and stored through `normalized_provider_user_id`.
- `password`: 8-128 characters.
- Passwords are stored only as PBKDF2-SHA256 hashes with `password_algorithm = 'pbkdf2-sha256'`.

Suggested request:

```json
{
  "gameId": "player_333",
  "password": "example_password",
  "displayName": "Player333",
  "clientVersion": "dev-build"
}
```

Suggested response:

```json
{
  "sessionToken": "server-session-token",
  "account": {
    "id": "uuid",
    "displayName": "Player333",
    "accountKind": "registered"
  },
  "wallet": {
    "resourceGold": 0,
    "tickets": 0
  }
}
```

### `POST /auth/login`

Purpose:

- Login with an existing first-party Game ID.
- Create a fresh API session for that account.

Suggested request:

```json
{
  "gameId": "player_333",
  "password": "example_password",
  "clientVersion": "dev-build"
}
```

### `POST /auth/link-game-id`

Purpose:

- Link a Game ID login method to the currently authenticated account.
- This is the first guest-to-registered upgrade path.
- Existing wallet, tickets, card collection, draft runs, rewards, and active server state remain under the same `account_id`.

Authorization:

```text
Authorization: Bearer {sessionToken}
```

Suggested request:

```json
{
  "gameId": "player_333",
  "password": "example_password",
  "displayName": "Player333"
}
```

Suggested response:

```json
{
  "account": {
    "id": "same-account-uuid",
    "displayName": "Player333",
    "accountKind": "registered"
  },
  "wallet": {
    "resourceGold": 0,
    "tickets": 0
  }
}
```

Server behavior:

- If `guestToken` is empty, generate a new guest token.
- Create an `accounts` row with `account_kind = guest`.
- Create an `auth_identities` row with `provider = guest`.
- Create a `user_wallets` row.
- Create an `auth_sessions` row with a hashed session token.
- Return the raw `guestToken` and raw `sessionToken` to Unity.
- If `guestToken` matches an existing guest identity, resume that account and create a new session.
- If the matched account is disabled, deleted, or banned, return an auth error.

Suggested response:

```json
{
  "sessionToken": "server-session-token",
  "guestToken": "stable-guest-resume-token",
  "account": {
    "id": "uuid",
    "displayName": "Guest1234",
    "accountKind": "guest"
  },
  "wallet": {
    "resourceGold": 0,
    "tickets": 0
  }
}
```

### `GET /me`

Purpose:

- Return authenticated account summary.

Authorization:

```http
Authorization: Bearer {sessionToken}
```

Server behavior:

- Hash the bearer token.
- Find a non-revoked `auth_sessions` row whose `expires_at` is in the future.
- Load the linked account and wallet.
- Update `auth_sessions.last_used_at`.
- Return a clear `401` if the session is missing, expired, revoked, or linked to an inactive account.

Suggested response:

```json
{
  "account": {
    "id": "uuid",
    "displayName": "Guest1234",
    "accountKind": "guest"
  },
  "wallet": {
    "resourceGold": 0,
    "tickets": 0
  },
  "collectionSummary": {
    "ownedCardKinds": 0
  },
  "activeRun": null
}
```

### `POST /draft/{runId}/complete-test-save-deck`

Temporary development endpoint only.

Purpose:

- Save a known 33-card deck while the real persisted draft flow is not fully implemented.

Remove or replace this endpoint when real draft persistence is implemented.

## Implementation Order

### Step 1: Add Database Configuration

- Add connection string reading from `PROJECT333_DB_CONNECTION`.
- Fail with a clear server startup message if the DB connection is required but missing.

### Step 2: Add Migration Runner

- Create `schema_migrations`.
- Run pending SQL files in order.
- Refuse to run if a migration partially fails.

### Step 3: Add Repositories

Minimum repositories:

- `AccountRepository`
- `AuthIdentityRepository`
- `AuthSessionRepository`
- `WalletRepository`
- `DeckRepository`

Minimum services:

- `GuestAuthService`
- `CurrentAccountService`

### Step 4: Add Guest Login

Server behavior:

- If no guest token is provided, create account, auth identity, wallet, and return tokens.
- If a valid guest token is provided, resume the existing account.
- If the token is invalid, return a clear auth failure.
- Always create a new `auth_sessions` row for a successful login.
- Store only the session token hash.
- Return the raw session token only in the response body.

### Step 5: Add `GET /me`

Server behavior:

- Resolve session token.
- Load account.
- Load wallet.
- Return active run summary if one exists.

### Step 6: Save/Load Draft Deck

Server behavior:

- Validate deck has exactly 33 cards.
- Validate every card id exists in server card definitions.
- Insert `decks`.
- Insert `deck_cards`.
- Link to `draft_runs.completed_deck_id` when the run system is ready.

## Verification Checklist

Current verification checklist:

- Server starts with PostgreSQL connection string.
- Migrations `0001` through `0007` each run once and are recorded in `schema_migrations`.
- Restarting the server does not reapply an already recorded migration.
- `POST /auth/guest` creates `accounts`, `auth_identities`, `user_wallets`, and `auth_sessions`.
- `GET /me` returns the same account.
- Expired or revoked sessions cannot call `GET /me`.
- A test deck can be saved and loaded in the same card order.
- `POST /auth/register` creates a registered account and allows login with the same Game ID.
- `POST /auth/link-game-id` keeps the same account id when linking a Game ID to a guest account.
- Draft picks and the exact current offer survive client restart.
- Run wins/losses, one-time reward claim, wallet changes, card rewards, and upgrades survive client restart.
- PvP matchmaking rows identify both authenticated accounts and seats.
- Reconnect within the grace period restores the same seat and latest battle state.
- With startup recovery enabled, a persisted snapshot can recreate a missing in-memory battle session.

Unity-facing verification:

- Start screen can create/resume guest account.
- Ticket count displayed on start screen comes from server response.
- Draft and DeckBuilding scenes restore account/run data from the server.
- Game ID register/login can create a session for the same account model as guest login.
- Linking Game ID to a guest account keeps the same `account_id`.
- Social login must preserve the same rule when provider integration is implemented.

## Decisions Still Needed Later

- Android Keystore and iOS Keychain storage are implemented; real-device migration verification and the user-facing lost-device/account-recovery policy are still needed.
- Exact pack contents and probabilities.
- Social login provider setup details for Naver, Kakao, and Google.
- Hosted PostgreSQL backup/restore schedule and migration rollback policy.
