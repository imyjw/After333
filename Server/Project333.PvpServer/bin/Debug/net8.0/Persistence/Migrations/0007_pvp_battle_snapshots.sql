begin;

create table if not exists pvp_battle_snapshots (
    id uuid primary key default gen_random_uuid(),
    match_id uuid not null references pvp_matches(id) on delete cascade,
    snapshot_version bigint not null,
    snapshot_reason text not null,
    state_json jsonb not null,
    created_at timestamptz not null default now(),
    constraint pvp_battle_snapshots_version_positive check (snapshot_version > 0),
    constraint pvp_battle_snapshots_reason_check check (
        snapshot_reason in (
            'battle_started',
            'client_command',
            'server_ai_action',
            'turn_timeout',
            'disconnect_forfeit',
            'reconnect_join',
            'manual'
        )
    ),
    constraint pvp_battle_snapshots_match_version_unique unique (match_id, snapshot_version)
);

create index if not exists pvp_battle_snapshots_match_created_idx
    on pvp_battle_snapshots(match_id, created_at desc);

create table if not exists pvp_battle_command_log (
    id uuid primary key default gen_random_uuid(),
    match_id uuid not null references pvp_matches(id) on delete cascade,
    client_sequence bigint null,
    connection_id text null,
    account_id uuid null references accounts(id) on delete set null,
    actor_seat text null,
    runtime_actor_id text null,
    command_type text not null,
    command_json jsonb not null,
    event_json jsonb null,
    accepted boolean not null default true,
    rejection_code text null,
    rejection_message text null,
    created_at timestamptz not null default now(),
    metadata jsonb not null default '{}'::jsonb,
    constraint pvp_battle_command_log_actor_seat_check check (
        actor_seat is null or actor_seat in ('PlayerA', 'PlayerB')
    ),
    constraint pvp_battle_command_log_runtime_actor_check check (
        runtime_actor_id is null or runtime_actor_id in ('Player', 'AI')
    ),
    constraint pvp_battle_command_log_rejection_check check (
        (accepted = true and rejection_code is null and rejection_message is null) or
        (accepted = false)
    )
);

create index if not exists pvp_battle_command_log_match_created_idx
    on pvp_battle_command_log(match_id, created_at);

create index if not exists pvp_battle_command_log_match_client_sequence_idx
    on pvp_battle_command_log(match_id, client_sequence)
    where client_sequence is not null;

insert into schema_migrations(version) values ('0007_pvp_battle_snapshots');

commit;
