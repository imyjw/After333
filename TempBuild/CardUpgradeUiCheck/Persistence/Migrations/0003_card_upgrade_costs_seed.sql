insert into card_upgrade_costs(
    level_from,
    level_to,
    required_copy_count,
    required_resource_gold
)
values
    (0, 1, 3, 3),
    (1, 2, 6, 6),
    (2, 3, 9, 9),
    (3, 4, 12, 12),
    (4, 5, 15, 15),
    (5, 6, 18, 18),
    (6, 7, 21, 21),
    (7, 8, 24, 24),
    (8, 9, 27, 27),
    (9, 10, 30, 30),
    (10, 11, 33, 33),
    (11, 12, 36, 36),
    (12, 13, 39, 39)
on conflict (level_from, level_to)
do update set
    required_copy_count = excluded.required_copy_count,
    required_resource_gold = excluded.required_resource_gold;
