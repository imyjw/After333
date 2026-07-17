using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.Online
{
    public enum OnlineBattleSessionPhase
    {
        Idle = 0,
        Matchmaking = 1,
        Reconnecting = 2,
        BattleReady = 3,
        BattleActive = 4,
        Cancelling = 5,
        Failed = 6,
        Ended = 7
    }

    [DisallowMultipleComponent]
    public sealed class OnlineBattleSessionCoordinator : MonoBehaviour
    {
        private const string PersistentObjectName = "OnlineBattleSessionCoordinator";

        private static OnlineBattleSessionCoordinator s_instance;

        private OnlineBattleConnectionTester _connectionTester;
        private OnlineBattleSessionPhase _phase = OnlineBattleSessionPhase.Idle;
        private string _lastError = string.Empty;

        public static OnlineBattleSessionCoordinator Instance => s_instance;

        public event Action<OnlineBattleSessionPhase> PhaseChanged;

        public OnlineBattleSessionPhase Phase => _phase;

        public OnlineBattleConnectionTester ConnectionTester => _connectionTester;

        public string LastError => _lastError;

        public bool HasAuthoritativeBattle =>
            _connectionTester != null && _connectionTester.HasAuthoritativeBattleState;

        public bool HasActiveBattle =>
            _connectionTester != null && _connectionTester.HasActiveAuthoritativeBattle;

        public bool CanSendBattleCommands =>
            HasActiveBattle && _connectionTester.IsReadyForCommands;

        public bool IsMatchmaking =>
            _phase == OnlineBattleSessionPhase.Matchmaking ||
            _phase == OnlineBattleSessionPhase.Reconnecting ||
            _phase == OnlineBattleSessionPhase.Cancelling;

        public static OnlineBattleSessionCoordinator GetOrCreate()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            var existing = FindFirstObjectByType<OnlineBattleSessionCoordinator>();
            if (existing != null)
            {
                s_instance = existing;
                return existing;
            }

            var sessionObject = new GameObject(PersistentObjectName);
            DontDestroyOnLoad(sessionObject);
            sessionObject.AddComponent<OnlineBattleConnectionTester>();
            return sessionObject.AddComponent<OnlineBattleSessionCoordinator>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _connectionTester = GetComponent<OnlineBattleConnectionTester>();
            if (_connectionTester == null)
            {
                _connectionTester = gameObject.AddComponent<OnlineBattleConnectionTester>();
            }
        }

        private void Update()
        {
            if (_connectionTester == null)
            {
                return;
            }

            if ((_phase == OnlineBattleSessionPhase.Matchmaking ||
                 _phase == OnlineBattleSessionPhase.Reconnecting) &&
                _connectionTester.HasAuthoritativeBattleState)
            {
                SetPhase(_connectionTester.HasActiveAuthoritativeBattle
                    ? OnlineBattleSessionPhase.BattleReady
                    : OnlineBattleSessionPhase.Ended);
                return;
            }

            if ((_phase == OnlineBattleSessionPhase.Matchmaking ||
                 _phase == OnlineBattleSessionPhase.Reconnecting) &&
                !string.IsNullOrWhiteSpace(_connectionTester.MatchmakingFailureMessage))
            {
                _lastError = _connectionTester.MatchmakingFailureMessage;
                SetPhase(OnlineBattleSessionPhase.Failed);
                return;
            }

            if (_phase == OnlineBattleSessionPhase.BattleActive &&
                _connectionTester.HasAuthoritativeBattleState &&
                !_connectionTester.HasActiveAuthoritativeBattle)
            {
                SetPhase(OnlineBattleSessionPhase.Ended);
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public void BeginPvpMatchmaking(
            string serverUrl,
            string playerToken,
            IReadOnlyList<string> playerDeckCardIds,
            bool isReconnect)
        {
            if (_connectionTester == null)
            {
                throw new InvalidOperationException("Online battle connection tester is missing.");
            }

            _lastError = string.Empty;
            _connectionTester.DetachBattlePresentation();
            SetPhase(isReconnect
                ? OnlineBattleSessionPhase.Reconnecting
                : OnlineBattleSessionPhase.Matchmaking);
            _connectionTester.ConnectForPvpMatchmaking(
                serverUrl,
                playerToken,
                playerDeckCardIds,
                presentStateViewsToBattleScreen: false);
        }

        public bool TryAttachToBattle(BattleBootstrapper battleBootstrapper)
        {
            if (battleBootstrapper == null || !HasActiveBattle)
            {
                return false;
            }

            _connectionTester.AttachToBattlePresentation(battleBootstrapper);
            SetPhase(OnlineBattleSessionPhase.BattleActive);
            return true;
        }

        public async Task<bool> CancelMatchmakingAndDisposeAsync()
        {
            if (HasActiveBattle)
            {
                SetPhase(OnlineBattleSessionPhase.BattleReady);
                return false;
            }

            SetPhase(OnlineBattleSessionPhase.Cancelling);
            if (_connectionTester != null)
            {
                await _connectionTester.DisconnectFromServerAsync();
            }

            if (HasActiveBattle)
            {
                SetPhase(OnlineBattleSessionPhase.BattleReady);
                return false;
            }

            SetPhase(OnlineBattleSessionPhase.Idle);
            if (this != null)
            {
                Destroy(gameObject);
            }

            return true;
        }

        private void SetPhase(OnlineBattleSessionPhase nextPhase)
        {
            if (_phase == nextPhase)
            {
                return;
            }

            _phase = nextPhase;
            PhaseChanged?.Invoke(_phase);
        }
    }
}
