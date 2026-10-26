begin;

alter table accounts add column active_login_family_id uuid null;
alter table auth_sessions add column login_family_id uuid null;
alter table auth_sessions add column superseded_at timestamptz null;

-- Existing refresh rotations already identify a logical login family. Refuse
-- ambiguous/orphan legacy access sessions instead of inventing another login.
update auth_sessions s set login_family_id=f.family_id
from (
    select session_id,(array_agg(token_family_id order by token_family_id))[1] as family_id
    from auth_refresh_tokens group by session_id having count(distinct token_family_id)=1
) f where f.session_id=s.id;

-- Keep the newest independent login that still has a usable access/refresh pair.
-- min(created_at) identifies family creation rather than a later token rotation.
with viable_families as (
    select rt.account_id,rt.token_family_id,min(rt.created_at) as family_created_at
    from auth_refresh_tokens rt group by rt.account_id,rt.token_family_id
    having bool_or(rt.revoked_at is null and rt.expires_at>now() and exists (
        select 1 from auth_sessions s where s.id=rt.session_id and s.account_id=rt.account_id
          and s.login_family_id=rt.token_family_id and s.revoked_at is null))
), latest as (
    select distinct on(account_id) account_id,token_family_id
    from viable_families order by account_id,family_created_at desc,token_family_id desc
)
update accounts a set active_login_family_id=l.token_family_id from latest l where a.id=l.account_id;

update auth_sessions s set revoked_at=coalesce(s.revoked_at,clock_timestamp()),
    superseded_at=case when s.login_family_id is not null then clock_timestamp() else null end
from accounts a where a.id=s.account_id
  and (s.login_family_id is null or s.login_family_id is distinct from a.active_login_family_id);
update auth_refresh_tokens rt set revoked_at=coalesce(rt.revoked_at,clock_timestamp())
from accounts a where a.id=rt.account_id and rt.token_family_id is distinct from a.active_login_family_id;

create index auth_sessions_login_family_idx on auth_sessions(account_id,login_family_id)
    where revoked_at is null;
insert into schema_migrations(version) values ('0025_single_login_family');
commit;
