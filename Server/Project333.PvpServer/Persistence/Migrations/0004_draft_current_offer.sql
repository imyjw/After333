begin;

alter table draft_runs
    add column if not exists current_offer_card_ids jsonb not null default '[]'::jsonb;

insert into schema_migrations(version) values ('0004_draft_current_offer');

commit;
