begin;

-- Success receipts have the same lifetime as the account. Do not expire them:
-- an old client retry must never become a new charge after server restart.
create table account_operation_receipts (
    account_id uuid not null references accounts(id) on delete cascade,
    request_id uuid not null check (request_id <> '00000000-0000-0000-0000-000000000000'),
    operation_type text not null check (operation_type in ('ticket_purchase', 'card_upgrade')),
    request_fingerprint text not null,
    response_json jsonb not null,
    created_at timestamptz not null default now(),
    primary key (account_id, request_id)
);

insert into schema_migrations(version) values ('0015_account_operation_receipts');
commit;
