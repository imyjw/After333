begin;
create table account_operation_cancellations (
    account_id uuid not null references accounts(id) on delete cascade,
    request_id uuid not null check(request_id <> '00000000-0000-0000-0000-000000000000'),
    created_at timestamptz not null default now(),
    primary key(account_id,request_id)
);
-- Keep tombstones for delayed requests and old-client retries.
insert into schema_migrations(version) values ('0016_account_operation_cancellations');
commit;
