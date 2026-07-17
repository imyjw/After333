begin;

alter table auth_identities
    add column if not exists normalized_provider_user_id text,
    add column if not exists password_algorithm text null,
    add column if not exists password_updated_at timestamptz null,
    add column if not exists email_verified_at timestamptz null,
    add column if not exists identity_status text not null default 'active',
    add column if not exists linked_at timestamptz not null default now(),
    add column if not exists revoked_at timestamptz null,
    add column if not exists external_profile jsonb not null default '{}'::jsonb,
    add column if not exists updated_at timestamptz not null default now();

update auth_identities
set normalized_provider_user_id = case
        when provider = 'game_id' then lower(trim(provider_user_id))
        else provider_user_id
    end
where normalized_provider_user_id is null;

alter table auth_identities
    alter column normalized_provider_user_id set not null;

alter table auth_identities
    drop constraint if exists auth_identities_identity_status_check;

alter table auth_identities
    add constraint auth_identities_identity_status_check
    check (identity_status in ('active', 'revoked'));

alter table auth_identities
    drop constraint if exists auth_identities_password_provider_check;

alter table auth_identities
    add constraint auth_identities_password_provider_check
    check (
        (provider = 'game_id' and password_hash is not null and password_algorithm is not null) or
        (provider <> 'game_id' and password_hash is null)
    );

alter table auth_identities
    drop constraint if exists auth_identities_revoked_after_created_check;

alter table auth_identities
    add constraint auth_identities_revoked_after_created_check
    check (revoked_at is null or revoked_at >= created_at);

alter table auth_identities
    drop constraint if exists auth_identities_provider_user_unique;

create unique index if not exists auth_identities_provider_normalized_user_unique
    on auth_identities(provider, normalized_provider_user_id)
    where identity_status = 'active';

create index if not exists auth_identities_active_account_idx
    on auth_identities(account_id, provider)
    where identity_status = 'active';

create table if not exists auth_identity_link_events (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    auth_identity_id uuid null references auth_identities(id) on delete set null,
    provider text not null,
    event_type text not null,
    previous_account_id uuid null references accounts(id) on delete set null,
    created_at timestamptz not null default now(),
    metadata jsonb not null default '{}'::jsonb,
    constraint auth_identity_link_events_provider_check check (provider in ('game_id', 'guest', 'naver', 'kakao', 'google')),
    constraint auth_identity_link_events_type_check check (event_type in ('created', 'linked', 'unlinked', 'login', 'password_changed'))
);

create index if not exists auth_identity_link_events_account_id_idx
    on auth_identity_link_events(account_id, created_at desc);

create index if not exists auth_identity_link_events_identity_id_idx
    on auth_identity_link_events(auth_identity_id, created_at desc);

insert into schema_migrations(version) values ('0005_auth_identity_login_expansion');

commit;
