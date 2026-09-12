begin;

-- A receipt and every affected run are committed in the same transaction.
create table battle_result_receipts (
    result_id uuid primary key,
    match_id text not null,
    payload jsonb not null,
    recorded_at timestamptz not null default now()
);
create index battle_result_receipts_match_idx on battle_result_receipts(match_id);

create table battle_run_results (
    result_id uuid not null references battle_result_receipts(result_id),
    seat text not null check (seat in ('PlayerA', 'PlayerB')),
    account_id uuid not null references accounts(id),
    run_id uuid not null references draft_runs(id),
    deck_id uuid not null references decks(id),
    won boolean not null,
    primary key (result_id, seat),
    unique (result_id, account_id),
    unique (result_id, run_id)
);

insert into schema_migrations(version) values ('0012_battle_result_receipts');
commit;
