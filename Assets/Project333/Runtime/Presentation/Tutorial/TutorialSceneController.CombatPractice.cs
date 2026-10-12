using System.Collections.Generic;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Runtime.Presentation.Tutorial
{
    public sealed partial class TutorialSceneController
    {
        private readonly HashSet<TileLongPressRelay> _combatPreviewRelays = new HashSet<TileLongPressRelay>();
        private TileTextView _lastCombatPreview;
        private Text _combatActionHint;
        private RectTransform _combatDamageComparison;
        private bool _combatArcherPlaced, _combatWasResolving;
        private void RefreshCombatDamageComparison()
        {
            var visible = _session.Chapter == 5 && CanPractice && _session.CurrentStep.Cue == TutorialCue.DamageTypes;
            if (visible && _combatDamageComparison == null)
            {
                _combatDamageComparison = Panel(_safeRoot, "DamageTypeComparison", new Vector2(.33f,.735f), new Vector2(.72f,.86f));
                _combatDamageComparison.GetComponent<Image>().raycastTarget = false;
                var colors = new[] { new Color(1f,.4f,.3f), new Color(.35f,.7f,1f), Color.white };
                var rows = new[] { "물리 피해  →  물리 방어력으로 감소", "마법 피해  →  마법 방어력으로 감소", "고정 피해  →  두 방어력 무시" };
                for (var i = 0; i < rows.Length; i++)
                {
                    var row = Label(_combatDamageComparison, "DamageType" + i, rows[i], 24, colors[i], TextAnchor.MiddleCenter);
                    Place(row.rectTransform, new Vector2(.04f, 1f-(i+1)/3f), new Vector2(.96f, 1f-i/3f));
                }
            }
            if (_combatDamageComparison != null) _combatDamageComparison.gameObject.SetActive(visible);
        }
        private void DetachCombatPreviewHandlers()
        {
            foreach (var relay in _combatPreviewRelays) if (relay != null) relay.PreviewReleased -= OnCombatPreviewReleased;
            _combatPreviewRelays.Clear(); _lastCombatPreview = null;
        }
        private void OnCombatPreviewReleased(TileView tile)
        {
            if (tile == null || !CanPractice || _session.CurrentStep.RequiredAction != TutorialAction.PreviewOccupant) return;
            _lastCombatPreview = tile.GetComponent<TileTextView>();
            _practiceBattle.TutorialOccupantPreviewReleased(tile);
        }
        private TileTextView CombatTileView(TileCoord coord, PlayerId owner = PlayerId.Player)
        {
            var board = _practiceBattle?.TutorialScreen?.BoardPresenter;
            if (board == null) return null;
            foreach (var tile in owner == PlayerId.Player ? board.PlayerTileViews : board.AITileViews)
                if (tile != null && tile.Coord.Equals(coord)) return tile.GetComponent<TileTextView>();
            return null;
        }
        private void RefreshCombatPracticeGuidance()
        {
            var placed = _practiceBattle.CurrentBattleState.PlayerBoard.GetOccupant(TutorialCombatBattle.ArcherTile)?.CardId == "SkeletonArcher";
            var resolving = _practiceBattle.IsTutorialResolving;
            if (placed != _combatArcherPlaced || resolving != _combatWasResolving)
            {
                _combatArcherPlaced = placed; _combatWasResolving = resolving;
                ConfigurePracticeHighlights();
            }
        }
        private void CombatPair(TileCoord source, TileCoord target, PlayerId targetOwner = PlayerId.AI)
        {
            AssignPracticeHighlight(0, PracticeTile(source));
            AssignPracticeHighlight(1, PracticeTile(target, targetOwner));
        }
        private void CombatHand(string cardId)
        {
            var image = PracticeHandArtwork(cardId);
            AssignPracticeHighlight(0, image?.rectTransform, image, matchArtworkSorting: true);
        }
        private void CombatPreview(TileTextView tile, Rect? artworkRegion = null, bool tooltips = false)
        {
            if (tile == null || !tile.TryShowCurrentCardPreview()) return;
            _practiceBattle.TutorialScreen?.TutorialHand?.SetExpanded(false);
            var image = TileTextView.VisibleCardPreviewArtwork;
            if (tooltips)
            {
                var tooltip = TileTextView.VisibleCardPreviewTooltip(_session.CurrentStep.Cue == TutorialCue.PreviewInfluences);
                if (tooltip != null) AssignPracticeHighlight(0, tooltip);
                else AssignPracticeHighlight(0, image?.rectTransform, image);
            }
            else AssignPracticeHighlight(0, image?.rectTransform, image, artworkRegion: artworkRegion);
        }
        private void ConfigureCombatPracticeHighlights()
        {
            var board = _practiceBattle.TutorialScreen?.BoardPresenter;
            if (board == null) return;
            foreach (var tile in board.PlayerTileViews) BindCombatPreview(tile);
            foreach (var tile in board.AITileViews) BindCombatPreview(tile);
            var action = _session.CurrentStep.RequiredAction;
            var cue = _session.CurrentStep.Cue;
            if (_combatActionHint == null)
            {
                var panel = _dialogue.rectTransform.parent;
                _combatActionHint = Label(panel, "PracticeActionHint", "", 18, new Color(.5f,1f,.3f), TextAnchor.MiddleCenter);
                Place(_combatActionHint.rectTransform, new Vector2(.885f,.12f), new Vector2(.985f,.66f));
            }
            _combatActionHint.gameObject.SetActive(_session.WaitingForAction && !_session.IsComplete);
            _combatActionHint.text = "실습";
            switch (action)
            {
                case TutorialAction.AttemptBlockedAttack:
                    CombatPair(TutorialResourceBattle.GoblinTile, TutorialResourceBattle.MasterTile); _combatActionHint.text = "고블린\n→\n마스터"; return;
                case TutorialAction.SummonArcherAndEndTurn:
                    if (_practiceBattle.CurrentBattleState.PlayerBoard.IsEmpty(TutorialCombatBattle.ArcherTile))
                    { CombatHand("SkeletonArcher"); AssignPracticeHighlight(1, PracticeTile(TutorialCombatBattle.ArcherTile)); _combatActionHint.text = "아처\n소환"; }
                    else { AssignPracticeHighlight(0, _practiceBattle.TutorialScreen.TutorialEndTurnRect); _combatActionHint.text = "턴 종료"; }
                    return;
                case TutorialAction.RangedMasterAttack:
                case TutorialAction.FinishPracticeBattle:
                    CombatPair(TutorialCombatBattle.FindPlayerArcher(_practiceBattle.CurrentBattleState), TutorialResourceBattle.MasterTile); _combatActionHint.text = "아처\n→\n마스터"; return;
                case TutorialAction.MeleeCounterattack:
                    CombatPair(TutorialResourceBattle.GoblinTile, TutorialCombatBattle.EnemyGoblinTile); _combatActionHint.text = "고블린\n→\n고블린"; return;
                case TutorialAction.EndTurnForGolem:
                    AssignPracticeHighlight(0, _practiceBattle.TutorialScreen.TutorialEndTurnRect); _combatActionHint.text = "턴 종료"; return;
                case TutorialAction.AttackGolem:
                    CombatPair(TutorialCombatBattle.ArcherTile, TutorialCombatBattle.GolemTile); _combatActionHint.text = "아처\n→\n골렘"; return;
                case TutorialAction.CastPracticeFireball:
                    CombatHand("firebolt"); AssignPracticeHighlight(1, PracticeTile(TutorialResourceBattle.MasterTile, PlayerId.AI)); _combatActionHint.text = "파이어볼\n→\n마스터"; return;
                case TutorialAction.PreviewOccupant:
                    var index = 0;
                    foreach (var tile in board.PlayerTileViews) if (tile?.IsOccupied == true) AssignPracticeHighlight(index++, PracticeTile(tile.Coord));
                    foreach (var tile in board.AITileViews) if (tile?.IsOccupied == true) AssignPracticeHighlight(index++, PracticeTile(tile.Coord, PlayerId.AI));
                    _combatActionHint.text = "꾹 누른 뒤\n손 떼기"; return;
                case TutorialAction.MovePracticeUnit:
                    CombatPair(TutorialCombatBattle.ArcherTile, TutorialCombatBattle.MovedArcherTile, PlayerId.Player); _combatActionHint.text = "아처\n이동"; return;
                case TutorialAction.SwapPracticeUnits:
                    CombatPair(TutorialCombatBattle.MovedArcherTile, TutorialResourceBattle.MasterTile, PlayerId.Player); _combatActionHint.text = "마스터\n↔\n아처"; return;
                case TutorialAction.AttemptBuildingMove:
                    CombatPair(TutorialResourceBattle.PondTile, TutorialCombatBattle.BuildingMoveTile, PlayerId.Player); _combatActionHint.text = "건물\n이동 시도"; return;
            }
            switch (cue)
            {
                case TutorialCue.CombatReady:
                case TutorialCue.CombatAttackLimit:
                    AssignPracticeHighlight(0, PracticeTile(TutorialResourceBattle.GoblinTile)); break;
                case TutorialCue.Frontline:
                case TutorialCue.FrontlineRules:
                case TutorialCue.BlockedAttackResult:
                    AssignPracticeHighlight(0, PracticeTile(TutorialCombatBattle.FrontlineTile, PlayerId.AI));
                    AssignPracticeHighlight(1, PracticeTile(TutorialResourceBattle.MasterTile, PlayerId.AI)); break;
                case TutorialCue.MeleeCounter:
                    CombatPair(TutorialResourceBattle.GoblinTile, TutorialCombatBattle.EnemyGoblinTile); break;
                case TutorialCue.AttackTypeIcon:
                case TutorialCue.DamageTypeColor:
                    CombatPreview(CombatTileView(TutorialCombatBattle.ArcherTile), new Rect(0f,.725f,.139f,.129f)); break;
                case TutorialCue.PhysicalDefense:
                case TutorialCue.MagicDefense:
                case TutorialCue.FixedDamage:
                    CombatPreview(CombatTileView(TutorialCombatBattle.GolemTile, PlayerId.AI), new Rect(.1f,.10f,.8f,.26f)); break;
                case TutorialCue.SpellPowerPractice:
                    CombatPreview(CombatTileView(TutorialResourceBattle.PondTile), tooltips: true); break;
                case TutorialCue.FireballIntro:
                case TutorialCue.FireballCost:
                    CombatHand("firebolt"); break;
                case TutorialCue.PreviewStatuses:
                case TutorialCue.PreviewInfluences:
                    CombatPreview(_lastCombatPreview != null ? _lastCombatPreview : CombatTileView(TutorialResourceBattle.PondTile), tooltips: true); break;
                case TutorialCue.VictorySetup:
                case TutorialCue.PracticeVictory:
                    AssignPracticeHighlight(0, PracticeTile(TutorialResourceBattle.MasterTile, PlayerId.AI)); break;
            }
        }
        private void BindCombatPreview(TileView tile)
        {
            if (tile == null) return;
            foreach (var relay in tile.GetComponentsInChildren<TileLongPressRelay>(true))
                if (_combatPreviewRelays.Add(relay)) relay.PreviewReleased += OnCombatPreviewReleased;
        }
    }
}
