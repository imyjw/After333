begin;

-- One immutable administrator dispatch. The caller-generated ID is its retry key.
create table gift_campaigns (
    id uuid primary key,
    request_hash text not null,
    title text not null,
    message text not null,
    tickets integer not null,
    resource_gold bigint not null,
    all_existing_accounts boolean not null,
    target_account_ids uuid[] not null,
    admin_reason text not null,
    recipient_count bigint not null default 0,
    created_at timestamptz not null default clock_timestamp(),
    expires_at timestamptz null,
    constraint gift_campaigns_request_hash_check check (length(request_hash) = 64),
    constraint gift_campaigns_title_check check (length(title) between 1 and 80),
    constraint gift_campaigns_message_check check (length(message) <= 1000),
    constraint gift_campaigns_reason_check check (length(admin_reason) between 1 and 300),
    constraint gift_campaigns_items_check check (tickets >= 0 and resource_gold >= 0 and (tickets > 0 or resource_gold > 0)),
    constraint gift_campaigns_targets_check check (
        (all_existing_accounts and cardinality(target_account_ids) = 0)
        or (not all_existing_accounts and cardinality(target_account_ids) > 0)),
    constraint gift_campaigns_recipient_count_check check (recipient_count >= 0),
    constraint gift_campaigns_expiry_check check (expires_at is null or expires_at > created_at)
);

-- This snapshot prevents a later registration from receiving an earlier all-account gift.
create table gift_inbox (
    gift_id uuid not null references gift_campaigns(id),
    account_id uuid not null references accounts(id) on delete cascade,
    delivered_at timestamptz not null,
    claimed_at timestamptz null,
    primary key (gift_id, account_id),
    constraint gift_inbox_claim_time_check check (claimed_at is null or claimed_at >= delivered_at)
);

create index gift_inbox_account_page_idx on gift_inbox(account_id, delivered_at desc, gift_id desc);
create index gift_inbox_account_unclaimed_idx on gift_inbox(account_id) where claimed_at is null;
create unique index account_transactions_gift_once_idx
    on account_transactions(account_id, source_id)
    where source_table = 'gift_inbox';

insert into schema_migrations(version) values ('0017_gift_inbox');
commit;
