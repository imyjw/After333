using System.Collections;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed partial class BattleBootstrapper
    {
        private bool _tutorialChapterFive, _tutorialFrontlinePrepared, _tutorialDeckEmptied, _tutorialFatiguePrepared, _tutorialVictoryPrepared;
        private Coroutine _tutorialCombatCompletion;
        private TutorialCombatLogRecorder _tutorialCombatLog;
        private void InitializeTutorialCombatLog()
        {
            _tutorialCombatLog = new TutorialCombatLogRecorder(CurrentBattleState);
            AppendCombatLogText(BattleCombatLogEntryFormatter.Format(new Project333.Runtime.Application.Online.BattleCombatLogEntryDto
            { EntryType = Project333.Runtime.Application.Online.BattleCombatLogEntryType.BattleStarted, SourceOwnerId = PlayerId.Player }, ResolveCombatLogCardDisplayName));
            if (CurrentBattleState.Phase == PhaseType.Main)
                AppendCombatLogText(BattleCombatLogEntryFormatter.Format(new Project333.Runtime.Application.Online.BattleCombatLogEntryDto
                { EntryType = Project333.Runtime.Application.Online.BattleCombatLogEntryType.TurnStarted,
                    SourceOwnerId = CurrentBattleState.ActivePlayerId, TurnNumber = CurrentBattleState.TurnNumber }, ResolveCombatLogCardDisplayName));
        }
        private void RecordTutorialCombatLog()
        {
            if (_tutorialCombatLog == null || CurrentBattleState == null) return;
            foreach (var entry in _tutorialCombatLog.Capture(CurrentBattleState))
                AppendCombatLogText(BattleCombatLogEntryFormatter.Format(entry, ResolveCombatLogCardDisplayName));
        }
        private void ResetTutorialCombatPractice()
        {
            if (_tutorialCombatCompletion != null) StopCoroutine(_tutorialCombatCompletion);
            _tutorialCombatCompletion = null; _tutorialCombatLog = null;
            _tutorialChapterFive = _tutorialFrontlinePrepared = _tutorialDeckEmptied = _tutorialFatiguePrepared = _tutorialVictoryPrepared = false;
        }
        private void ConfigureTutorialCombatStage()
        {
            var cue = _tutorialSession.CurrentStep.Cue;
            if (!_tutorialFrontlinePrepared && cue == TutorialCue.Frontline)
            {
                _tutorialFrontlinePrepared = true;
                TutorialCombatBattle.PrepareFrontline(CurrentBattleState, _cardDefinitionProvider);
                _tutorialCombatLog?.Rebase(CurrentBattleState);
                RefreshPresenter();
            }
            if (!_tutorialDeckEmptied && cue == TutorialCue.DeckExhaustion)
            {
                _tutorialDeckEmptied = true; TutorialCombatBattle.EmptyPracticeDeck(CurrentBattleState); _tutorialCombatLog?.Rebase(CurrentBattleState); RefreshPresenter();
            }
            if (!_tutorialFatiguePrepared && cue == TutorialCue.DrawFailureFirst)
            {
                _tutorialFatiguePrepared = true;
                StartPresentationSequence(TutorialFatigueDemo());
            }
            if (!_tutorialVictoryPrepared && cue == TutorialCue.VictorySetup)
            {
                _tutorialVictoryPrepared = true; TutorialCombatBattle.PrepareVictory(CurrentBattleState); _tutorialCombatLog?.Rebase(CurrentBattleState); RefreshPresenter();
            }
        }
        private bool TutorialAllowsAttack(TileCoord from, TileCoord to)
        {
            if (!_tutorialBattleEntry) return true;
            if (!_tutorialChapterFive) return false;
            if (TutorialActionAllowed(TutorialAction.AttemptBlockedAttack))
                return from.Equals(TutorialResourceBattle.GoblinTile) && to.Equals(TutorialResourceBattle.MasterTile);
            if (TutorialActionAllowed(TutorialAction.MeleeCounterattack))
                return from.Equals(TutorialResourceBattle.GoblinTile) && to.Equals(TutorialCombatBattle.EnemyGoblinTile);
            if (TutorialActionAllowed(TutorialAction.RangedMasterAttack) || TutorialActionAllowed(TutorialAction.FinishPracticeBattle))
                return from.Equals(TutorialCombatBattle.FindPlayerArcher(CurrentBattleState)) && to.Equals(TutorialResourceBattle.MasterTile);
            return TutorialActionAllowed(TutorialAction.AttackGolem) && from.Equals(TutorialCombatBattle.ArcherTile) && to.Equals(TutorialCombatBattle.GolemTile);
        }
        private void TutorialAttackAccepted()
        {
            if (_tutorialChapterFive) QueueTutorialCombatCompletion(_tutorialSession.CurrentStep.RequiredAction);
        }
        private void TutorialAttackRejected(TileCoord from, TileCoord to)
        {
            if (!TutorialActionAllowed(TutorialAction.AttemptBlockedAttack) || !TutorialAllowsAttack(from, to)) return;
            var source = CurrentBattleState.PlayerBoard.GetOccupant(from);
            if (source == null || source.HasSummoningSickness || source.RemainingAttacksThisTurn < 1) return;
            if (!new TargetingService().CanTarget(CurrentBattleState, PlayerId.Player, from, to))
                _tutorialCompleted?.Invoke(TutorialAction.AttemptBlockedAttack);
        }
        public bool TutorialAllowsImmobileSelection(OccupantState occupant) => _tutorialChapterFive &&
            TutorialActionAllowed(TutorialAction.AttemptBuildingMove) && occupant?.CardId == "ManaPond";
        private bool TutorialAllowsMove(TileCoord from, TileCoord to)
        {
            if (!_tutorialBattleEntry) return true;
            return _tutorialChapterFive &&
                ((TutorialActionAllowed(TutorialAction.MovePracticeUnit) && from.Equals(TutorialCombatBattle.ArcherTile) && to.Equals(TutorialCombatBattle.MovedArcherTile)) ||
                (TutorialActionAllowed(TutorialAction.SwapPracticeUnits) &&
                    ((from.Equals(TutorialCombatBattle.MovedArcherTile) && to.Equals(TutorialResourceBattle.MasterTile)) ||
                     (from.Equals(TutorialResourceBattle.MasterTile) && to.Equals(TutorialCombatBattle.MovedArcherTile)))) ||
                (TutorialActionAllowed(TutorialAction.AttemptBuildingMove) && from.Equals(TutorialResourceBattle.PondTile) && to.Equals(TutorialCombatBattle.BuildingMoveTile)));
        }
        private void TutorialMoveAccepted()
        {
            if (TutorialActionAllowed(TutorialAction.MovePracticeUnit)) _tutorialCompleted?.Invoke(TutorialAction.MovePracticeUnit);
            else if (TutorialActionAllowed(TutorialAction.SwapPracticeUnits) &&
                CurrentBattleState.PlayerBoard.GetOccupant(TutorialCombatBattle.MovedArcherTile)?.Kind == OccupantKind.Master &&
                CurrentBattleState.PlayerBoard.GetOccupant(TutorialResourceBattle.MasterTile)?.CardId == "SkeletonArcher")
                _tutorialCompleted?.Invoke(TutorialAction.SwapPracticeUnits);
        }
        private void TutorialMoveRejected(TileCoord from, TileCoord to)
        {
            if (TutorialActionAllowed(TutorialAction.AttemptBuildingMove) && TutorialAllowsMove(from, to) &&
                CurrentBattleState.PlayerBoard.GetOccupant(from)?.CanMove == false)
                _tutorialCompleted?.Invoke(TutorialAction.AttemptBuildingMove);
        }
        public void TutorialOccupantPreviewReleased(TileView tile)
        {
            if (tile?.CurrentOccupant != null && TutorialActionAllowed(TutorialAction.PreviewOccupant))
                _tutorialCompleted?.Invoke(TutorialAction.PreviewOccupant);
        }
        private void QueueTutorialCombatCompletion(TutorialAction action)
        {
            if (_tutorialCombatCompletion != null || !_tutorialChapterFive) return;
            _tutorialResolving = true;
            _tutorialCombatCompletion = StartCoroutine(CompleteTutorialCombatAfterPresentation(action));
        }
        private IEnumerator CompleteTutorialCombatAfterPresentation(TutorialAction action)
        {
            yield return null; // Never complete synchronously before the coroutine handle is assigned.
            while (_isBusy || _presentationSequenceCoroutine != null) yield return null;
            _tutorialResolving = false;
            while (_tutorialCanInteract?.Invoke() != true) yield return null;
            _tutorialCombatCompletion = null;
            _tutorialCompleted?.Invoke(action);
        }
        private IEnumerator TutorialFatigueDemo()
        {
            _tutorialResolving = true;
            _battleGateway.EndTurn(); RecordTutorialCombatLog();
            _battleGateway.ResolveTurnStart(); RecordTutorialCombatLog();
            _battleGateway.EndTurn(); RecordTutorialCombatLog();
            var before = CaptureBattleSnapshot(CurrentBattleState);
            _battleGateway.ResolveTurnStart();
            RecordTutorialCombatLog();
            var impact = BuildImpactAnimationRequest(before, CaptureBattleSnapshot(CurrentBattleState), CloneValuePopupEvents(CurrentBattleState));
            if (HasPresentationImpact(impact)) yield return PlayImpactAnimationSequence(impact);
            RefreshPresenter(); _tutorialResolving = false;
        }
    }
}
