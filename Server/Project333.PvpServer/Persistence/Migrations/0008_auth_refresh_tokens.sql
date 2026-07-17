begin;

create table if not exists auth_refresh_tokens (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    session_id uuid not null references auth_sessions(id) on delete cascade,
    token_family_id uuid not null,
    refresh_token_hash text not null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    last_used_at timestamptz null,
    revoked_at timestamptz null,
    replaced_by_token_id uuid null references auth_refresh_tokens(id) on delete set null,
    user_agent text null,
    client_version text null,
    ip_address inet null,
    constraint auth_refresh_tokens_hash_unique unique (refresh_token_hash),
    constraint auth_refresh_tokens_expires_after_created check (expires_at > created_at),
    constraint auth_refresh_tokens_revoked_after_created check (revoked_at is null or revoked_at >= created_at)
);

create index if not exists auth_refresh_tokens_active_lookup_idx
    on auth_refresh_tokens(refresh_token_hash, expires_at)
    where revoked_at is null;

create index if not exists auth_refresh_tokens_account_idx
    on auth_refresh_tokens(account_id, created_at desc);

create index if not exists auth_refresh_tokens_family_idx
    on auth_refresh_tokens(token_family_id, created_at desc);

create index if not exists auth_refresh_tokens_session_idx
    on auth_refresh_tokens(session_id);

insert into schema_migrations(version) values ('0008_auth_refresh_tokens');

commit;
