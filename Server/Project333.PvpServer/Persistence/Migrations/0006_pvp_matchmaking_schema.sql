begin;

create table if not exists pvp_matches (
    id uuid primary key default gen_random_uuid(),
    match_id text not null,
    match_status text not null default 'waiting',
    matchmaking_region text not null default 'default',
    client_version text null,
    winner_account_id uuid null references accounts(id) on delete set null,
    ended_reason text null,
    turn_timeout_seconds integer not null default 73,
    reconnect_grace_seconds integer not null default 60,
    created_at timestamptz not null default now(),
    started_at timestamptz null,
    completed_at timestamptz null,
    updated_at timestamptz not null default now(),
    metadata jsonb not null default '{}'::jsonb,
    constraint pvp_matches_match_id_unique unique (match_id),
    constraint pvp_matches_status_check check (match_status in ('waiting', 'active', 'reconnect_grace', 'completed', 'canceled')),
    constraint pvp_matches_ended_reason_check check (
        ended_reason is null or ended_reason in ('normal', 'forfeit', 'reconnect_timeout', 'server_cancel', 'error')
    ),
    constraint pvp_matches_turn_timeout_positive check (turn_timeout_seconds > 0),
    constraint pvp_matches_reconnect_grace_nonnegative check (reconnect_grace_seconds >= 0),
    constraint pvp_matches_winner_completed_check check (winner_account_id is null or match_status = 'completed'),
    constraint pvp_matches_completed_at_check check (completed_at is null or completed_at >= created_at),
    constraint pvp_matches_started_at_check check (started_at is null or started_at >= created_at)
);

create index if not exists pvp_matches_status_updated_idx
    on pvp_matches(match_status, updated_at desc);

create index if not exists pvp_matches_winner_account_id_idx
    on pvp_matches(winner_account_id, completed_at desc)
    where winner_account_id is not null;

create table if not exists pvp_match_queue (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    run_id uuid null references draft_runs(id) on delete set null,
    deck_id uuid null references decks(id) on delete set null,
    queue_status text not null default 'queued',
    matchmaking_region text not null default 'default',
    client_version text null,
    matched_match_id uuid null references pvp_matches(id) on delete set null,
    requested_at timestamptz not null default now(),
    matched_at timestamptz null,
    canceled_at timestamptz null,
    expires_at timestamptz null,
    updated_at timestamptz not null default now(),
    metadata jsonb not null default '{}'::jsonb,
    constraint pvp_match_queue_status_check check (queue_status in ('queued', 'matched', 'canceled', 'expired')),
    constraint pvp_match_queue_matched_consistency_check check (
        (queue_status = 'matched' and matched_match_id is not null and matched_at is not null) or
        (queue_status <> 'matched')
    ),
    constraint pvp_match_queue_canceled_consistency_check check (
        (queue_status = 'canceled' and canceled_at is not null) or
        (queue_status <> 'canceled')
    )
);

create unique index if not exists pvp_match_queue_one_active_request_per_account_idx
    on pvp_match_queue(account_id)
    where queue_status = 'queued';

create index if not exists pvp_match_queue_available_idx
    on pvp_match_queue(matchmaking_region, requested_at)
    where queue_status = 'queued';

create index if not exists pvp_match_queue_account_idx
    on pvp_match_queue(account_id, requested_at desc);

create index if not exists pvp_match_queue_matched_match_idx
    on pvp_match_queue(matched_match_id)
    where matched_match_id is not null;

create table if not exists pvp_match_players (
    match_id uuid not null references pvp_matches(id) on delete cascade,
    seat text not null,
    account_id uuid not null references accounts(id) on delete cascade,
    run_id uuid null references draft_runs(id) on delete set null,
    deck_id uuid null references decks(id) on delete set null,
    runtime_player_id text not null,
    player_status text not null default 'active',
    joined_at timestamptz not null default now(),
    last_seen_at timestamptz null,
    disconnected_at timestamptz null,
    reconnect_deadline_at timestamptz null,
    final_result text null,
    session_version integer not null default 0,
    metadata jsonb not null default '{}'::jsonb,
    primary key (match_id, seat),
    constraint pvp_match_players_match_account_unique unique (match_id, account_id),
    constraint pvp_match_players_seat_check check (seat in ('PlayerA', 'PlayerB')),
    constraint pvp_match_players_runtime_player_id_check check (runtime_player_id in ('Player', 'AI')),
    constraint pvp_match_players_status_check check (player_status in ('active', 'disconnected', 'forfeited', 'winner', 'loser')),
    constraint pvp_match_players_final_result_check check (final_result is null or final_result in ('win', 'loss', 'draw')),
    constraint pvp_match_players_session_version_nonnegative check (session_version >= 0),
    constraint pvp_match_players_reconnect_deadline_check check (
        reconnect_deadline_at is null or disconnected_at is not null
    )
);

create index if not exists pvp_match_players_account_idx
    on pvp_match_players(account_id, joined_at desc);

create index if not exists pvp_match_players_reconnectable_idx
    on pvp_match_players(account_id, reconnect_deadline_at)
    where player_status = 'disconnected' and reconnect_deadline_at is not null;

create index if not exists pvp_match_players_match_status_idx
    on pvp_match_players(match_id, player_status);

create table if not exists pvp_match_connections (
    id uuid primary key default gen_random_uuid(),
    match_id uuid not null references pvp_matches(id) on delete cascade,
    seat text not null,
    account_id uuid not null references accounts(id) on delete cascade,
    connection_id text not null,
    connection_status text not null default 'active',
    connected_at timestamptz not null default now(),
    last_seen_at timestamptz null,
    disconnected_at timestamptz null,
    remote_endpoint text null,
    user_agent text null,
    metadata jsonb not null default '{}'::jsonb,
    constraint pvp_match_connections_player_fk foreign key (match_id, seat)
        references pvp_match_players(match_id, seat) on delete cascade,
    constraint pvp_match_connections_seat_check check (seat in ('PlayerA', 'PlayerB')),
    constraint pvp_match_connections_status_check check (connection_status in ('active', 'closed', 'replaced', 'rejected')),
    constraint pvp_match_connections_closed_at_check check (
        (connection_status = 'active' and disconnected_at is null) or
        (connection_status <> 'active')
    )
);

create unique index if not exists pvp_match_connections_active_account_idx
    on pvp_match_connections(match_id, account_id)
    where connection_status = 'active';

create unique index if not exists pvp_match_connections_active_connection_id_idx
    on pvp_match_connections(connection_id)
    where connection_status = 'active';

create index if not exists pvp_match_connections_match_idx
    on pvp_match_connections(match_id, connected_at desc);

create index if not exists pvp_match_connections_account_idx
    on pvp_match_connections(account_id, connected_at desc);

insert into schema_migrations(version) values ('0006_pvp_matchmaking_schema');

commit;
