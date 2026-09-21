begin;

alter table pvp_matches
    add column opponent_kind text not null default 'human'
        check (opponent_kind in ('human','ai')),
    add column ai_fallback_at timestamptz null,
    add column ai_fallback_account_id uuid null references accounts(id);

insert into schema_migrations(version) values ('0019_matchmaking_ai_fallback');

commit;
