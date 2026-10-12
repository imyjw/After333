begin;

-- Deploy together with the ElfElder server and client revision, with old writers stopped.
-- Preserve quantities, levels, slots, request IDs, outcomes, and all wallet balances.
lock table user_card_collection in share row exclusive mode;

-- If both spellings exist, retain every unspent copy and the highest earned level.
update user_card_collection current_card
set copy_count = current_card.copy_count + legacy.copy_count,
    upgrade_level = greatest(current_card.upgrade_level, legacy.upgrade_level),
    created_at = least(current_card.created_at, legacy.created_at),
    updated_at = greatest(current_card.updated_at, legacy.updated_at)
from user_card_collection legacy
where current_card.account_id = legacy.account_id
  and current_card.card_id = 'ElfElder' and legacy.card_id = 'Elf Elder';

delete from user_card_collection legacy
using user_card_collection current_card
where legacy.account_id = current_card.account_id
  and legacy.card_id = 'Elf Elder' and current_card.card_id = 'ElfElder';

update user_card_collection set card_id = 'ElfElder' where card_id = 'Elf Elder';
update deck_cards set card_id = 'ElfElder' where card_id = 'Elf Elder';
update draft_run_picks set selected_card_id = 'ElfElder' where selected_card_id = 'Elf Elder';
update account_transactions set card_id = 'ElfElder' where card_id = 'Elf Elder';

-- JSON arrays, object keys and string values are changed by exact ID, never substrings.
-- A mixed-key object is ambiguous (count versus upgrade level); abort atomically
-- rather than silently discard either value. Inspect that case before retrying.
create function pg_temp.rename_elf_elder_id(value jsonb) returns jsonb
language plpgsql as $$
declare
    renamed jsonb;
begin
    case jsonb_typeof(value)
    when 'string' then
        return case when value = to_jsonb('Elf Elder'::text)
            then to_jsonb('ElfElder'::text) else value end;
    when 'array' then
        select coalesce(jsonb_agg(pg_temp.rename_elf_elder_id(item) order by position), '[]'::jsonb)
        into renamed from jsonb_array_elements(value) with ordinality as items(item, position);
        return renamed;
    when 'object' then
        if value ? 'Elf Elder' and value ? 'ElfElder' then
            raise exception 'ElfElder JSON key collision; migration rolled back for manual inspection';
        end if;
        select coalesce(jsonb_object_agg(
            case when key = 'Elf Elder' then 'ElfElder' else key end,
            pg_temp.rename_elf_elder_id(item)), '{}'::jsonb)
        into renamed from jsonb_each(value) as items(key, item);
        return renamed;
    else
        return value;
    end case;
end;
$$;

-- Include persisted offers, reward/pack receipts, participant metadata, snapshots
-- and command/event history. Only JSON columns containing the exact legacy ID change.
do $$
declare field record;
begin
    for field in
        select table_name, column_name from information_schema.columns
        where table_schema = 'public' and data_type = 'jsonb'
        order by table_name, ordinal_position
    loop
        execute format('update public.%I set %I = pg_temp.rename_elf_elder_id(%I) where position(%L in %I::text) > 0',
            field.table_name, field.column_name, field.column_name, '"Elf Elder"', field.column_name);
    end loop;
end;
$$;

-- Fingerprints are exact compact JSON text: preserve formatting and all other fields
-- so a retried canonical upgrade request still replays its existing success receipt.
update account_operation_receipts
set request_fingerprint = replace(request_fingerprint, '"Elf Elder"', '"ElfElder"')
where operation_type = 'card_upgrade' and position('"Elf Elder"' in request_fingerprint) > 0;

drop function pg_temp.rename_elf_elder_id(jsonb);
insert into schema_migrations(version) values ('0024_elf_elder_card_id');
commit;
