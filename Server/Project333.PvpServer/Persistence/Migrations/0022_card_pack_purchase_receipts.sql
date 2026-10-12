begin;

-- Original receipts and cancellation tombstones retain their account lifetime.
-- Add the new product to the existing one-transaction purchase receipt protocol.
alter table account_operation_receipts
    drop constraint if exists account_operation_receipts_operation_type_check;
alter table account_operation_receipts
    add constraint account_operation_receipts_operation_type_check
    check (operation_type in ('ticket_purchase', 'card_upgrade', 'card_pack_purchase'));

insert into schema_migrations(version) values ('0022_card_pack_purchase_receipts')
on conflict(version) do nothing;
commit;
