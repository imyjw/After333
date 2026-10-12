begin;

-- Only future Lv.1 -> Lv.2 upgrades change. Previously paid costs and all
-- other level transitions, wallets, collection, ledgers and receipts remain.
insert into card_upgrade_costs (
    level_from,
    level_to,
    required_copy_count,
    required_resource_gold
)
values (1, 2, 6, 6)
on conflict (level_from, level_to)
do update set
    required_copy_count = excluded.required_copy_count,
    required_resource_gold = excluded.required_resource_gold;

insert into schema_migrations (version)
values ('0021_card_upgrade_level2_cost_revision')
on conflict (version) do nothing;

commit;
