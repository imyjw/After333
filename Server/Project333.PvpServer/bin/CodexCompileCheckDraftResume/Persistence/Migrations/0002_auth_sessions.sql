begin;

create table if not exists auth_sessions (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    session_token_hash text not null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    last_used_at timestamptz null,
    user_agent text null,
    client_version text null,
    ip_address inet null,
    constraint auth_sessions_token_hash_unique unique (session_token_hash),
    constraint auth_sessions_expires_after_created check (expires_at > created_at),
    constraint auth_sessions_revoked_after_created check (revoked_at is null or revoked_at >= created_at)
);

create index if not exists auth_sessions_account_id_idx on auth_sessions(account_id);
create index if not exists auth_sessions_active_lookup_idx
    on auth_sessions(session_token_hash, expires_at)
    where revoked_at is null;
create index if not exists auth_sessions_account_active_idx
    on auth_sessions(account_id, expires_at)
    where revoked_at is null;

insert into schema_migrations(version) values ('0002_auth_sessions');

commit;
