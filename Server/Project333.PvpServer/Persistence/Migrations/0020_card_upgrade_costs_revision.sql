begin;

-- Future upgrades use the confirmed target-level costs. Existing collection,
-- wallets, ledgers and committed operation receipts are intentionally untouched.
insert into card_upgrade_costs (
    level_from,
    level_to,
    required_copy_count,
    required_resource_gold
)
values
    (0, 1, 3, 3),
    (1, 2, 5, 5),
    (2, 3, 10, 10),
    (3, 4, 20, 20),
    (4, 5, 30, 30),
    (5, 6, 33, 33),
    (6, 7, 33, 33),
    (7, 8, 33, 33),
    (8, 9, 33, 33),
    (9, 10, 33, 33),
    (10, 11, 33, 33),
    (11, 12, 33, 33),
    (12, 13, 33, 33)
on conflict (level_from, level_to)
do update set
    required_copy_count = excluded.required_copy_count,
    required_resource_gold = excluded.required_resource_gold;

insert into schema_migrations (version)
values ('0020_card_upgrade_costs_revision')
on conflict (version) do nothing;

commit;
