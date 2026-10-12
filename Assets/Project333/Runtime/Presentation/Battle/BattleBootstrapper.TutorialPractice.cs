using System;
using System.Collections;
using System.Collections.Generic;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Tutorial;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed partial class BattleBootstrapper
    {
        private TutorialSession _tutorialSession;
        private Func<bool> _tutorialCanInteract;
        private Action<TutorialAction> _tutorialCompleted;
        private Action _tutorialSelectionCanceled;
        private bool _tutorialChapterFour, _tutorialResolving, _tutorialGrantApplied;
        private Coroutine _tutorialMulliganRoutine;
        private int _tutorialRevision = -1;
        public bool IsTutorialResolving => _tutorialResolving;
        public int TutorialGoldGrant { get; private set; }
        public int TutorialManaGrant { get; private set; }
        public BattleScreenPresenter TutorialScreen => _battleScreenPresenter;
        public bool TutorialAllowsEndTurn => !_tutorialBattleEntry ||
            (_tutorialChapterFour && (TutorialActionAllowed(TutorialAction.EndTurn) ||
                (_tutorialChapterFive && (TutorialActionAllowed(TutorialAction.EndTurnForGolem) ||
                (TutorialActionAllowed(TutorialAction.SummonArcherAndEndTurn) && !CurrentBattleState.PlayerBoard.IsEmpty(TutorialCombatBattle.ArcherTile))))));
        private bool TutorialActionAllowed(TutorialAction action) => _tutorialSession != null &&
            !_tutorialResolving && _tutorialCanInteract?.Invoke() == true && _tutorialSession.WaitingForAction &&
            _tutorialSession.CurrentStep.RequiredAction == action;

        public void ConfigureTutorialChapterFour(TutorialSession session, Func<bool> canInteract,
            Action<TutorialAction> completed, Action selectionCanceled)
        {
            if (!TutorialRuntimeIsolation.IsActive || session == null || (session.Chapter != 4 && session.Chapter != 5))
                throw new InvalidOperationException("Chapter four requires its isolated tutorial host.");
            _tutorialCanInteract = canInteract; _tutorialCompleted = completed; _tutorialSelectionCanceled = selectionCanceled;
            if (!_tutorialChapterFour || _tutorialSession != session || _tutorialRevision != session.Revision)
            {
                CancelPresentationSequence();
                ResetTutorialCombatPractice();
                if (_tutorialMulliganRoutine != null) StopCoroutine(_tutorialMulliganRoutine);
                _tutorialMulliganRoutine = null;
                _tutorialSession = session; _tutorialRevision = session.Revision;
                _tutorialBattleEntry = true; _tutorialChapterFour = true; _tutorialResolving = false; _tutorialGrantApplied = false;
                TutorialGoldGrant = 0; TutorialManaGrant = 0; _autoRunAiTurns = false; _returnToDraftSceneWhenRunSessionActive = false;
                _endTurnSubmittedBattle = null; _endTurnSubmittedNumber = -1;
                ConfigureLocalInput();
                _cardDefinitionProvider = _cardCatalogAsset.CreateProvider();
                _battleGateway = session.Chapter == 5 ? TutorialCombatBattle.Create(_cardDefinitionProvider) : TutorialResourceBattle.Create(_cardDefinitionProvider);
                _battleScreenPresenter?.ResetTutorialPractice();
                _battleScreenPresenter?.Bind(this); _battleScreenPresenter?.SetBattleUiVisible(true);
                _battleScreenPresenter?.TutorialHand?.SetExpanded(false);
                _battleScreenPresenter?.TutorialHand?.ConfigureTutorialExpansion(() => TutorialHandInputAllowed(null));
                ClearCombatLog(); InitializeTutorialCombatLog();
                RefreshPresenter();
            }
            _tutorialChapterFive = session.Chapter == 5;
            if (_tutorialChapterFive) ConfigureTutorialCombatStage();
            if (!_tutorialGrantApplied && session.CurrentStep.Cue == TutorialCue.PracticeGrant)
            {
                _tutorialGrantApplied = true;
                var grant = TutorialResourceBattle.GrantPracticeResources(CurrentBattleState);
                TutorialGoldGrant = grant.Gold; TutorialManaGrant = grant.Mana;
                AddCombatLogEntry("연습 자원: 마나 +" + TutorialManaGrant + " · 골드 +" + TutorialGoldGrant);
                RefreshPresenter();
            }
        }
        public bool TutorialAllowsMulliganToggle(string cardId) => !_tutorialBattleEntry ||
            (cardId == "firebolt" && (TutorialActionAllowed(TutorialAction.SelectMulliganCard) || TutorialActionAllowed(TutorialAction.ConfirmMulligan)));
        public void TutorialMulliganSelectionChanged(bool selected)
        {
            if (!_tutorialChapterFour) return;
            if (selected && TutorialActionAllowed(TutorialAction.SelectMulliganCard))
                _tutorialCompleted?.Invoke(TutorialAction.SelectMulliganCard);
            else if (!selected && TutorialActionAllowed(TutorialAction.ConfirmMulligan)) _tutorialSelectionCanceled?.Invoke();
        }
        private bool SubmitTutorialMulligan(IReadOnlyList<string> cards)
        {
            if (!TutorialActionAllowed(TutorialAction.ConfirmMulligan) || cards == null || cards.Count != 1 || cards[0] != "firebolt" ||
                CurrentBattleState?.Phase != PhaseType.Mulligan || CurrentBattleState.Player.HasUsedMulligan) return false;
            _tutorialResolving = true;
            _battleGateway.ApplyMulligan(PlayerId.Player, cards, new TutorialResourceBattle.MulliganDeck());
            RefreshPresenter();
            _tutorialMulliganRoutine = StartCoroutine(CompleteTutorialMulligan());
            return true;
        }
        private IEnumerator CompleteTutorialMulligan()
        {
            // Wait for the real replacement flight and result display, not an arbitrary lesson timer.
            while (IsMulliganPresentationActive) yield return null;
            _battleGateway.ResolveTurnStart();
            AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
            RefreshPresenter();
            _tutorialResolving = false;
            while (_tutorialCanInteract?.Invoke() != true) yield return null;
            _tutorialMulliganRoutine = null;
            _tutorialCompleted?.Invoke(TutorialAction.ConfirmMulligan);
        }
        public bool TutorialHandInputAllowed(string cardId)
        {
            if (!_tutorialBattleEntry) return true;
            if (!_tutorialChapterFour || _tutorialResolving || _tutorialCanInteract?.Invoke() != true || !_tutorialSession.WaitingForAction) return false;
            var action = _tutorialSession.CurrentStep.RequiredAction;
            if (_tutorialChapterFive) return (action == TutorialAction.SummonArcherAndEndTurn && CurrentBattleState.PlayerBoard.IsEmpty(TutorialCombatBattle.ArcherTile) && (cardId == null || cardId == "SkeletonArcher")) ||
                (action == TutorialAction.CastPracticeFireball && (cardId == null || cardId == "firebolt"));
            if (action == TutorialAction.ExpandHand) return true;
            if (action == TutorialAction.SelectGoblin || action == TutorialAction.SummonGoblin)
                return cardId == null || cardId == "Goblin";
            if (action == TutorialAction.SelectManaPond || action == TutorialAction.SummonManaPond)
                return cardId == null || cardId == "ManaPond";
            return false;
        }
        public void TutorialHandInputCompleted(bool expanded, string selectedCardId)
        {
            if (!_tutorialChapterFour) return;
            if (expanded && TutorialActionAllowed(TutorialAction.ExpandHand)) _tutorialCompleted?.Invoke(TutorialAction.ExpandHand);
            else if (selectedCardId == "Goblin" && TutorialActionAllowed(TutorialAction.SelectGoblin)) _tutorialCompleted?.Invoke(TutorialAction.SelectGoblin);
            else if (selectedCardId == "ManaPond" && TutorialActionAllowed(TutorialAction.SelectManaPond)) _tutorialCompleted?.Invoke(TutorialAction.SelectManaPond);
        }
        private bool TutorialAllowsCardUse(string cardId, PlayerId owner, TileCoord coord)
        {
            if (!_tutorialBattleEntry) return true;
            if (_tutorialChapterFive) return
                (cardId == "SkeletonArcher" && owner == PlayerId.Player && coord.Equals(TutorialCombatBattle.ArcherTile) && TutorialActionAllowed(TutorialAction.SummonArcherAndEndTurn) && CurrentBattleState.PlayerBoard.IsEmpty(coord)) ||
                (cardId == "firebolt" && owner == PlayerId.AI && coord.Equals(TutorialResourceBattle.MasterTile) && TutorialActionAllowed(TutorialAction.CastPracticeFireball));
            return owner == PlayerId.Player &&
                ((cardId == "Goblin" && coord.Equals(TutorialResourceBattle.GoblinTile) && TutorialActionAllowed(TutorialAction.SummonGoblin)) ||
                 (cardId == "ManaPond" && coord.Equals(TutorialResourceBattle.PondTile) && TutorialActionAllowed(TutorialAction.SummonManaPond)));
        }
        private void TutorialCardUsed(string cardId)
        {
            if (_tutorialChapterFive)
            {
                if (cardId == "firebolt" && TutorialActionAllowed(TutorialAction.CastPracticeFireball)) QueueTutorialCombatCompletion(TutorialAction.CastPracticeFireball);
                return;
            }
            if (cardId == "Goblin" && TutorialActionAllowed(TutorialAction.SummonGoblin)) _tutorialCompleted?.Invoke(TutorialAction.SummonGoblin);
            else if (cardId == "ManaPond" && TutorialActionAllowed(TutorialAction.SummonManaPond)) _tutorialCompleted?.Invoke(TutorialAction.SummonManaPond);
        }
        private IEnumerator TutorialOpponentTurnSequence()
        {
            var completedAction = _tutorialSession.CurrentStep.RequiredAction;
            _tutorialResolving = true;
            _battleGateway.ResolveTurnStart();
            AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.AI, CurrentBattleState.TurnNumber));
            RefreshPresenter();
            yield return new WaitForSecondsRealtime(.4f);
            var command = _tutorialChapterFive ? TutorialCombatBattle.OpponentSummon(CurrentBattleState, completedAction == TutorialAction.EndTurnForGolem) :
                new PlayUnitCardCommand("SkeletonArcher", TutorialResourceBattle.OpponentArcherTile);
            var commandLog = CombatLogFormatter.FormatCommand(PlayerId.AI, command,
                CurrentBattleState, ResolveCombatLogCardDisplayName);
            _battleGateway.ExecuteCommand(PlayerId.AI, command);
            AddCombatLogEntry(commandLog);
            RefreshPresenter();
            yield return new WaitForSecondsRealtime(.3f);
            _battleGateway.EndTurn();
            AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(PlayerId.AI));
            var start = CaptureBattleSnapshot(CurrentBattleState);
            _battleGateway.ResolveTurnStart();
            AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
            var impact = BuildImpactAnimationRequest(start, CaptureBattleSnapshot(CurrentBattleState), CloneValuePopupEvents(CurrentBattleState));
            if (HasPresentationImpact(impact)) yield return PlayImpactAnimationSequence(impact);
            RefreshPresenter();
            _tutorialResolving = false;
            while (_tutorialCanInteract?.Invoke() != true) yield return null;
            _tutorialCompleted?.Invoke(completedAction);
        }
    }
}
