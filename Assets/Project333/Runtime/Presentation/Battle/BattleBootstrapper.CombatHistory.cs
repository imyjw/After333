using System.Collections.Generic;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Tutorial;
using Project333.Runtime.Domain.Battle;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed partial class BattleBootstrapper
    {
        private readonly List<BattleCombatLogEntryDto> _combatLogPreviewEntries = new List<BattleCombatLogEntryDto>();
        private TutorialCombatLogRecorder _localAttackRecorder;
        private BattleState _localAttackLogBattle;

        public bool TryGetCombatLogAttackPreview(int index, out BattleCombatLogEntryDto entry)
        {
            entry = index >= 0 && index < _combatLogPreviewEntries.Count ? _combatLogPreviewEntries[index] : null;
            return entry != null && entry.HasHoldPreview;
        }
        private void ResetCombatLogPreviews()
        {
            _combatLogPreviewEntries.Clear(); _localAttackRecorder = null; _localAttackLogBattle = null;
        }
        private void EnsureLocalCombatLogCapture()
        {
            var battle = CurrentBattleState;
            if (_tutorialBattleEntry || _sendPlayerActionsToOnlineGateway || battle == null) return;
            if (!ReferenceEquals(_localAttackLogBattle, battle))
            {
                _localAttackLogBattle = battle;
                _localAttackRecorder = new TutorialCombatLogRecorder(battle, _cardDefinitionProvider);
            }
        }
        private void PrimeCombatLogAttack()
        {
            if (_tutorialBattleEntry) _tutorialCombatLog?.Rebase(CurrentBattleState);
            else { EnsureLocalCombatLogCapture(); _localAttackRecorder?.Rebase(CurrentBattleState); }
        }
        private BattleCombatLogEntryDto CaptureLocalCombatLogAttack()
        {
            EnsureLocalCombatLogCapture();
            if (_sendPlayerActionsToOnlineGateway || _localAttackRecorder == null) return null;
            BattleCombatLogEntryDto fallback = null;
            foreach (var record in _localAttackRecorder.Capture(CurrentBattleState))
            {
                if (record.EntryType == BattleCombatLogEntryType.Attack) return record;
                if (fallback == null && record.HasHoldPreview) fallback = record;
            }
            return fallback;
        }
    }
}
