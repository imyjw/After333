begin;

alter table account_transactions
    drop constraint account_transactions_type_check;

alter table account_transactions
    add constraint account_transactions_type_check check (
        transaction_type in (
            'reward',
            'pack_open',
            'purchase',
            'craft',
            'upgrade',
            'ticket_spend',
            'admin_adjustment',
            'ad_reward')
    );

create table rewarded_ad_attempts (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    provider text not null,
    placement text not null,
    provider_user_id text not null,
    opaque_user_token varchar(64) not null unique,
    reward_ticket_count integer not null default 1,
    attempt_status text not null default 'pending',
    failure_code text null,
    expires_at timestamptz not null,
    rewarded_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint rewarded_ad_attempts_reward_positive check (reward_ticket_count > 0),
    constraint rewarded_ad_attempts_status_check check (
        attempt_status in ('pending', 'granted', 'expired', 'rejected'))
);

create index rewarded_ad_attempts_account_created_idx
    on rewarded_ad_attempts(account_id, created_at desc);

create index rewarded_ad_attempts_account_rewarded_idx
    on rewarded_ad_attempts(account_id, rewarded_at desc)
    where rewarded_at is not null;

create table rewarded_ad_events (
    id uuid primary key default gen_random_uuid(),
    provider text not null,
    provider_event_id text not null,
    attempt_id uuid null references rewarded_ad_attempts(id) on delete set null,
    account_id uuid null references accounts(id) on delete set null,
    provider_user_id text not null,
    reward_amount integer not null,
    placement text null,
    app_key text null,
    signature_verified boolean not null default false,
    processing_result text not null,
    received_at timestamptz not null default now(),
    processed_at timestamptz null,
    constraint rewarded_ad_events_provider_event_unique unique (provider, provider_event_id),
    constraint rewarded_ad_events_reward_nonnegative check (reward_amount >= 0)
);

create index rewarded_ad_events_attempt_idx on rewarded_ad_events(attempt_id);
create index rewarded_ad_events_account_received_idx
    on rewarded_ad_events(account_id, received_at desc);

insert into schema_migrations(version) values ('0009_rewarded_ads');

commit;
