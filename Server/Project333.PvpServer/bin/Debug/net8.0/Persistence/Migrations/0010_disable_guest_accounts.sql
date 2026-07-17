begin;

update auth_refresh_tokens refresh_tokens
   set revoked_at = coalesce(refresh_tokens.revoked_at, now()),
       last_used_at = now()
  from accounts
 where accounts.id = refresh_tokens.account_id
   and accounts.account_kind = 'guest';

update auth_sessions sessions
   set revoked_at = coalesce(sessions.revoked_at, now())
  from accounts
 where accounts.id = sessions.account_id
   and accounts.account_kind = 'guest';

update auth_identities identities
   set identity_status = 'revoked',
       updated_at = now()
 where identities.provider = 'guest';

update accounts
   set account_status = 'disabled',
       updated_at = now()
 where account_kind = 'guest'
   and account_status = 'active';

insert into schema_migrations(version) values ('0010_disable_guest_accounts');

commit;
