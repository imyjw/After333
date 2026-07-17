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
