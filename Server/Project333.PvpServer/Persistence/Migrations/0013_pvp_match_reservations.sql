begin;
create table pvp_match_reservations (
    id uuid primary key default gen_random_uuid(),
    match_id uuid not null references pvp_matches(id) on delete cascade,
    seat text not null check (seat in ('PlayerA', 'PlayerB')),
    account_id uuid not null references accounts(id) on delete cascade,
    connection_id text not null,
    run_id uuid null references draft_runs(id) on delete cascade,
    deck_id uuid null references decks(id) on delete cascade,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    unique (match_id, seat),
    unique (account_id)
);
create index pvp_match_reservations_expiry_idx on pvp_match_reservations(expires_at);
insert into schema_migrations(version) values ('0013_pvp_match_reservations');
commit;
