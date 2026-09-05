using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;
using UnityEngine.Events;

namespace Project333.Runtime.Presentation.HUD
{
    public sealed class ResourceBarPresenter : MonoBehaviour
    {
        [System.Serializable]
        public sealed class ResourceSummaryChangedEvent : UnityEvent<string, string, string>
        {
        }

        [SerializeField] private string _turnSummary = "No active battle.";
        [SerializeField] private string _playerResourceSummary = "M:0 Q:0 P:0 G:0";
        [SerializeField] private string _aiResourceSummary = "M:0 Q:0 P:0 G:0";
        [SerializeField] private ResourceSummaryChangedEvent _onResourceSummaryChanged = new ResourceSummaryChangedEvent();

        public string TurnSummary => _turnSummary;

        public string PlayerResourceSummary => _playerResourceSummary;

        public string AIResourceSummary => _aiResourceSummary;

        public void Present(BattleState battleState)
        {
            if (battleState == null)
            {
                _turnSummary = "No active battle.";
                _playerResourceSummary = "M:0 Q:0 P:0 G:0";
                _aiResourceSummary = "M:0 Q:0 P:0 G:0";
            }
            else
            {
                _turnSummary = $"Turn {battleState.TurnNumber} / {battleState.Phase} / {battleState.ActivePlayerId}";
                _playerResourceSummary = BattleUiFormatter.FormatResources(battleState.Player.Resources);
                _aiResourceSummary = BattleUiFormatter.FormatResources(battleState.AI.Resources);
            }

            _onResourceSummaryChanged.Invoke(_turnSummary, _playerResourceSummary, _aiResourceSummary);
        }
    }
}
