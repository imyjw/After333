begin;

alter table pvp_match_players
    drop constraint if exists pvp_match_players_status_check;

alter table pvp_match_players
    add constraint pvp_match_players_status_check
    check (player_status in ('active', 'disconnected', 'forfeited', 'winner', 'loser', 'draw'));

insert into schema_migrations(version) values ('0011_pvp_draw_player_status');

commit;
