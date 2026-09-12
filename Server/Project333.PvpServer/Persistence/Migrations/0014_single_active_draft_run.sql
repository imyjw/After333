begin;

-- Do not choose, delete or abandon existing runs automatically. If legacy active
-- duplicates exist, index creation fails and the entire migration rolls back.
create unique index draft_runs_one_active_per_account_idx
    on draft_runs(account_id)
    where status in ('drafting', 'ready', 'in_progress');

insert into schema_migrations(version) values ('0014_single_active_draft_run');
commit;
