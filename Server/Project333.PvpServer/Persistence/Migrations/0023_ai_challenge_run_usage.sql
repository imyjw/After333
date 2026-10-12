begin;

-- Original run_id is already durable for every admission, including legacy daily admissions.
-- Do not reset or rewrite attempts, refunds, receipts, or paid run data.
create index ai_challenge_run_usage_idx on ai_challenge_attempts(account_id, run_id)
    where refunded_at is null;

insert into schema_migrations(version) values ('0023_ai_challenge_run_usage');
commit;
