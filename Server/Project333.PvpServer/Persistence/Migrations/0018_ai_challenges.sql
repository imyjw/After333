begin;

-- Each accepted challenge button request consumes its original Korean calendar day.
-- Refunds mark that row; they never add attempts to a newer day's bucket.
create table ai_challenge_attempts (
    id uuid primary key,
    account_id uuid not null references accounts(id) on delete cascade,
    request_id uuid not null,
    run_id uuid not null references draft_runs(id),
    deck_id uuid not null references decks(id),
    match_id text not null unique,
    challenge_day date not null,
    status text not null default 'reserved'
        check (status in ('reserved', 'active', 'completed', 'canceled')),
    created_at timestamptz not null default clock_timestamp(),
    joined_at timestamptz null,
    completed_at timestamptz null,
    ended_reason text null,
    refunded_at timestamptz null,
    result_id uuid null unique references battle_result_receipts(result_id),
    won boolean null,
    resource_gold_awarded bigint not null default 0 check (resource_gold_awarded in (0, 10)),
    unique (account_id, request_id),
    constraint ai_challenge_refund_check check (
        refunded_at is null or (status = 'canceled' and ended_reason = 'server_cancel')),
    constraint ai_challenge_result_check check (
        (status = 'completed' and result_id is not null and won is not null)
        or (status <> 'completed' and result_id is null and won is null and resource_gold_awarded = 0))
);
-- Concurrent new request keys that attach to an existing attempt remain replayable after completion.
create table ai_challenge_requests (
    account_id uuid not null references accounts(id) on delete cascade,
    request_id uuid not null,
    attempt_id uuid not null references ai_challenge_attempts(id) on delete cascade,
    primary key (account_id, request_id)
);
create index ai_challenge_daily_usage_idx on ai_challenge_attempts(account_id, challenge_day)
    where refunded_at is null;
create unique index ai_challenge_one_pending_account_idx on ai_challenge_attempts(account_id)
    where status in ('reserved', 'active');
create unique index account_transactions_ai_challenge_once_idx
    on account_transactions(account_id, source_id) where source_table = 'ai_challenge_attempts';

insert into schema_migrations(version) values ('0018_ai_challenges');
commit;
