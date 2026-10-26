using System;
using Project333.Runtime.Presentation.Hand;
using Project333.Runtime.Presentation.Tutorial;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Online;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Project333.Runtime.Presentation.Battle
{
    public sealed partial class BattleBootstrapper : MonoBehaviour, IDraftOverlayHost
    {
        [System.Serializable]
        public sealed class CombatLogChangedEvent : UnityEvent<string>
        {
        }

        // Temporary bodies belong only to views, never to the authoritative board.
        private int _pendingOnlineSummonTrapImpacts;
        private int _pendingOnlineSummonTrapGroups;
        public bool IsSummonTrapOnlinePresentationPending => _pendingOnlineSummonTrapImpacts > 0 || _pendingOnlineSummonTrapGroups > 0;
        private readonly List<TileView> _summonTrapVisualTiles = new List<TileView>();
        private const int MaxCombatLogEntries = 33;
        private const string BusyMessage = "Battle is still resolving animations.";
        private const string MasterCardAssetResourcePath = "Project333/SpecialCards/MasterCard";
        private const string FireboltSpellEffectId = "firebolt";
        private const string FireboltSpellEffectResourcePath = "Project333/SpellEffects/Firebolt-Sheet";
        private const string FirewallSpellEffectId = "firewall";
        private const string FirewallSpellEffectResourcePath = "Project333/SpellEffects/Firewall-Sheet";
        private const string BiochemicalBombSpellEffectId = "biochemical_bomb";
        private const string BiochemicalBombSpellEffectResourcePath = "Project333/SpellEffects/BiochemicalBomb-Sheet";
        private const int FireboltSpellEffectFrameCount = 7;
        private const float FireboltSpellEffectFramesPerSecond = 14f;
        private const float OnlineErrorToastFadeInSeconds = 0.08f;
        private const float OnlineErrorToastFadeOutSeconds = 0.25f;
        private const float OnlineErrorToastVisibleSeconds = 2.2f;
        private const float ServerRunResultSyncTimeoutSeconds = 4f;
        private const int DefaultOpponentPlayedCardSortingOrder = 1000;
        private const int LegacyOpponentPlayedCardSortingOrder = 20005;
        private const string YourTurnBannerResourcePath = "Project333/BattleUI/YourTurnBanner";
        private static readonly Vector2 FireboltSpellEffectSize = new Vector2(220f, 220f);
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Arial Unicode MS",
            "Noto Sans CJK KR",
            "Noto Sans KR"
        };
        private static readonly string OnlineClientTokenSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        private static IReadOnlyList<Sprite> s_fireboltSpellEffectFrames;
        private static IReadOnlyList<Sprite> s_firewallSpellEffectFrames;
        private static IReadOnlyList<Sprite> s_biochemicalBombSpellEffectFrames;

        [SerializeField] private BattleScreenPresenter _battleScreenPresenter;
        [SerializeField] private CardDefinitionCatalogAsset _cardCatalogAsset;
        [SerializeField] private bool _useJsonCardDefinitions = true;
        [SerializeField] private DeckDefinitionAsset _playerDeckAsset;
        [SerializeField] private DeckDefinitionAsset _aiDeckAsset;
        [SerializeField] private string[] _playerDeckCardIds = Array.Empty<string>();
        [SerializeField] private string[] _aiDeckCardIds = Array.Empty<string>();
        [SerializeField] private bool _useGeneratedDebugDecksWhenEmpty = true;
        [Min(BattleDebugDeckFactory.MinimumDeckSize)]
        [SerializeField] private int _generatedDeckSize = BattleDebugDeckFactory.MinimumDeckSize;
        [SerializeField] private bool _startOnAwake = true;
        [SerializeField] private bool _autoPassPlayerMulligan;
        [SerializeField] private bool _autoResolveTurnStartAfterAutoPass = true;
        [SerializeField] private bool _autoRunAiTurns = true;
        [SerializeField] private bool _autoResolvePlayerTurnStartAfterAi = true;
        [Header("Draft")]
        [SerializeField] private bool _startWithDraftBeforeBattle;
        [SerializeField] private DraftOverlayPresenter _draftOverlayPresenter;
        [SerializeField] private bool _useFixedDraftSeed;
        [SerializeField] private int _fixedDraftSeed = 333;
        [SerializeField] private bool _autoStartBattleAfterDraft;
        [Header("Scene Flow")]
        [SerializeField] private bool _returnToDraftSceneWhenRunSessionActive = true;
        [SerializeField] private float _returnToDraftSceneDelaySeconds = 1.5f;
        [Header("Online")]
        [SerializeField] private bool _sendPlayerActionsToOnlineGateway;
        [SerializeField] private OnlineBattleConnectionTester _onlineConnectionTester;
        [SerializeField] private string _onlineServerUrl = "ws://127.0.0.1:7333/battle";
        [SerializeField] private string _onlineMatchIdPrefix = "draft-run";
        [SerializeField] private string _onlinePlayerToken = "player-a";
        [SerializeField] private bool _animateOnlineMoveEvents;
        [Header("Tile Area Spell Effects")]
        [SerializeField] private Sprite[] _firewallTileEffectFrames = Array.Empty<Sprite>();
        [SerializeField] [Min(0.01f)] private float _firewallTileEffectFramesPerSecond = 12f;
        [SerializeField] private Vector2 _firewallTileEffectSize = new Vector2(220f, 220f);
        [SerializeField] private Sprite[] _biochemicalBombTileEffectFrames = Array.Empty<Sprite>();
        [SerializeField] [Min(0.01f)] private float _biochemicalBombTileEffectFramesPerSecond = 12f;
        [SerializeField] private Vector2 _biochemicalBombTileEffectSize = new Vector2(220f, 220f);
        [Header("Online Error Toast")]
        [SerializeField] private Font _onlineErrorToastFont;
        [SerializeField] private Vector2 _onlineErrorToastAnchor = new Vector2(0.5f, 0.86f);
        [SerializeField] private Vector2 _onlineErrorToastOffset = Vector2.zero;
        [SerializeField] private Vector2 _onlineErrorToastSize = new Vector2(900f, 72f);
        [SerializeField] private Vector2 _onlineErrorToastTextPadding = new Vector2(28f, 8f);
        [SerializeField] private float _onlineErrorToastFontSize = 26f;
        [SerializeField] private Color _onlineErrorToastPanelColor = new Color(0.36f, 0.04f, 0.035f, 0.94f);
        [SerializeField] private Color _onlineStatusToastPanelColor = new Color(0.05f, 0.15f, 0.30f, 0.94f);
        [SerializeField] private Color _onlineSuccessToastPanelColor = new Color(0.04f, 0.26f, 0.14f, 0.94f);
        [SerializeField] private Color _onlineErrorToastTextColor = new Color(1f, 0.92f, 0.86f, 1f);
        [Header("Online Turn Timer")]
        [SerializeField] private Vector2 _onlineTurnTimerAnchor = new Vector2(0.5f, 0.965f);
        [SerializeField] private Vector2 _onlineTurnTimerOffset = new Vector2(0f, -20f);
        [SerializeField] private Vector2 _onlineTurnTimerSize = new Vector2(360f, 54f);
        [SerializeField] private Vector2 _onlineTurnTimerTextPadding = new Vector2(20f, 6f);
        [SerializeField] private float _onlineTurnTimerFontSize = 28f;
        [SerializeField] private int _onlineTurnTimerRevealThresholdSeconds = 20;
        [SerializeField] private int _onlineTurnTimerDangerThresholdSeconds = 10;
        [SerializeField] private Color _onlineTurnTimerPlayerPanelColor = new Color(0.05f, 0.22f, 0.13f, 0.92f);
        [SerializeField] private Color _onlineTurnTimerOpponentPanelColor = new Color(0.08f, 0.10f, 0.18f, 0.90f);
        [SerializeField] private Color _onlineTurnTimerDangerPanelColor = new Color(0.42f, 0.05f, 0.035f, 0.95f);
        [SerializeField] private Color _onlineTurnTimerTextColor = new Color(1f, 0.96f, 0.84f, 1f);
        [SerializeField] private Color _onlineTurnTimerDangerTextColor = new Color(1f, 0.78f, 0.62f, 1f);
        [Header("Your Turn Banner")]
        [SerializeField] private Sprite _yourTurnBannerSprite;
        [SerializeField] private Vector2 _yourTurnBannerSize = new Vector2(760f, 254f);
        [SerializeField] private Vector2 _yourTurnBannerOffset = Vector2.zero;
        [SerializeField] [Min(0f)] private float _yourTurnBannerFadeInSeconds = 0.15f;
        [SerializeField] [Min(0f)] private float _yourTurnBannerVisibleSeconds = 1.4f;
        [SerializeField] [Min(0f)] private float _yourTurnBannerFadeOutSeconds = 0.2f;
        [SerializeField] private int _yourTurnBannerSortingOrder = 5500;
        [Header("Opponent Played Card Reveal")]
        [SerializeField] private Vector2 _opponentPlayedCardRevealSize = new Vector2(320f, 440f);
        [SerializeField] [Min(0.1f)] private float _opponentPlayedCardRevealVisualScale = 1.2f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardNormalDisplaySeconds = 3f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardQueuedDisplaySeconds = 2f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardFadeInSeconds = 0.15f;
        [SerializeField] [Min(0f)] private float _opponentPlayedCardFadeOutSeconds = 0.15f;
        [SerializeField] private int _opponentPlayedCardSortingOrder = DefaultOpponentPlayedCardSortingOrder;
        [SerializeField] private bool _showOpponentPlayedCardStats = true;
        [SerializeField] private Vector2 _opponentPlayedCardAttackStatNormalizedPosition = HandCardStatOverlayLayout.DefaultAttackPosition;
        [SerializeField] private Vector2 _opponentPlayedCardHpStatNormalizedPosition = HandCardStatOverlayLayout.DefaultHpPosition;
        [SerializeField] private Vector2 _opponentPlayedCardStatTextSize = new Vector2(72f, 54f);
        [SerializeField] private int _opponentPlayedCardStatFontSize = 34;
        [SerializeField] private Color _opponentPlayedCardStatTextColor = Color.white;
        [SerializeField] private Color _opponentPlayedCardStatOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        [SerializeField] private Vector2 _opponentPlayedCardStatOutlineDistance = new Vector2(2f, -2f);
        [Header("PvP Matchmaking Overlay")]
        [SerializeField] private Vector2 _pvpMatchmakingPanelSize = new Vector2(680f, 300f);
        [SerializeField] private Vector2 _pvpMatchmakingPanelOffset = Vector2.zero;
        [SerializeField] private Vector2 _pvpMatchmakingTextPadding = new Vector2(42f, 72f);
        [SerializeField] private Vector2 _pvpMatchmakingCancelButtonSize = new Vector2(220f, 64f);
        [SerializeField] private Vector2 _pvpMatchmakingCancelButtonOffset = new Vector2(0f, 42f);
        [SerializeField] private float _pvpMatchmakingTextFontSize = 30f;
        [SerializeField] private float _pvpMatchmakingCancelFontSize = 24f;
        [SerializeField] private Color _pvpMatchmakingDimColor = new Color(0.01f, 0.015f, 0.025f, 0.72f);
        [SerializeField] private Color _pvpMatchmakingPanelColor = new Color(0.045f, 0.075f, 0.12f, 0.96f);
        [SerializeField] private Color _pvpMatchmakingPanelOutlineColor = new Color(0.65f, 0.78f, 1f, 0.52f);
        [SerializeField] private Color _pvpMatchmakingTextColor = new Color(0.94f, 0.98f, 1f, 1f);
        [SerializeField] private Color _pvpMatchmakingCancelButtonColor = new Color(0.22f, 0.23f, 0.28f, 1f);
        [SerializeField] private string _pvpMatchmakingWaitingText = "상대를 찾는 중입니다...";
        [SerializeField] private string _pvpMatchmakingFoundText = "상대를 찾았습니다!";
        [SerializeField] private string _pvpMatchmakingConnectionFailedText = "서버 연결에 실패했습니다. 나중에 다시 시도해주세요.";
        [SerializeField] private string _pvpMatchmakingCancelButtonLabel = "취소";
        [SerializeField] private string _pvpMatchmakingReturnButtonLabel = "돌아가기";
        [Header("Attack Animation")]
        [SerializeField] private bool _animateAttackSequences = true;
        [SerializeField] private float _attackRunDuration = 0.18f;
        [SerializeField] private float _attackImpactDelay = 0.04f;
        [SerializeField] private float _attackReactionDuration = 0.10f;
        [SerializeField] private float _attackReturnDuration = 0.18f;
        [SerializeField] private float _deathHoldDuration = 0.45f;
        [SerializeField] private float _valuePopupCascadeDelay = 0.18f;
        [SerializeField] private float _meleeAttackContactOffset = 36f;
        [SerializeField] private float _meleeAttackSideOffset = 24f;
        [SerializeField] private float _rangedAttackNudgeDistance = 28f;
        [SerializeField] private float _rangedAttackNudgeDuration = 0.08f;
        [SerializeField] private CombatLogChangedEvent _onCombatLogChanged = new CombatLogChangedEvent();
        [TextArea(6, 16)]
        [SerializeField] private string _combatLogText = "전투 기록이 없습니다.";
        [TextArea(2, 6)]
        [SerializeField] private string _lastInteractionStatus = "No interaction yet.";

        private BattleFlowController _battleFlowController;
        private IBattleGateway _battleGateway;
        private ICardDefinitionProvider _cardDefinitionProvider;
        private AiDecisionService _aiDecisionService;
        private AiTurnRunner _aiTurnRunner;
        private DraftSessionService _draftSessionService;
        private Coroutine _presentationSequenceCoroutine;
        private int _combatPresentationGeneration;
        private BlueDragonWaterCannon _blueDragonWaterCannon;
        private GwangmaCrossSlash _gwangmaCrossSlash;
        private RedDragonFireBreath _redDragonFireBreath;
        private RedDragonEndTurnFlamesVfx _redDragonEndTurnFlamesVfx;
        private int _pendingOnlineRedDragonImpacts;
        private int _pendingOnlineRedDragonGroups;
        public bool IsRedDragonOnlinePresentationPending => _pendingOnlineRedDragonImpacts > 0 || _pendingOnlineRedDragonGroups > 0;
        private A212SawAttack _a212SawAttack;
        private A301LaserAttack _a301LaserAttack;
        private GaebangChiefCoinPunch _gaebangChiefCoinPunch;
        private ManaCannonProjectile _manaCannonProjectile;
        private NuclearExplosionVfx _nuclearExplosionVfx;
        private TimedBombPresentationVfx _timedBombPresentationVfx;
        private FirewallVfx _firewallVfx;
        private EarthquakeVfx _earthquakeVfx;
        private MeteorVfx _meteorVfx;
        private FireSpearVfx _fireSpearVfx;
        private PlasmaShieldVfx _plasmaShieldVfx;
        private IronDomeMissileVfx _ironDomeMissileVfx;
        private int _pendingOnlineIronDomeImpacts;
        private int _pendingOnlineIronDomeGroups;
        public bool IsIronDomeOnlinePresentationPending => _pendingOnlineIronDomeImpacts > 0 || _pendingOnlineIronDomeGroups > 0;
        private ReactiveArmourVfx _reactiveArmourVfx;
        private int _pendingOnlineReactiveArmourImpacts;
        private int _pendingOnlineReactiveArmourGroups;
        public bool IsReactiveArmourOnlinePresentationPending => _pendingOnlineReactiveArmourImpacts > 0 || _pendingOnlineReactiveArmourGroups > 0;
        private int _pendingOnlinePlasmaShieldImpacts;
        private int _pendingOnlinePlasmaShieldGroups;
        public bool IsPlasmaShieldOnlinePresentationPending => _pendingOnlinePlasmaShieldImpacts > 0 || _pendingOnlinePlasmaShieldGroups > 0;
        private ManaShieldVfx _manaShieldVfx;
        private int _pendingOnlineManaShieldImpacts;
        private int _pendingOnlineManaShieldGroups;
        public bool IsManaShieldOnlinePresentationPending => _pendingOnlineManaShieldImpacts > 0 || _pendingOnlineManaShieldGroups > 0;
        private int _pendingOnlineFireSpearImpacts;
        private int _pendingOnlineFireSpearGroups;
        public bool IsFireSpearOnlinePresentationPending => _pendingOnlineFireSpearImpacts > 0 || _pendingOnlineFireSpearGroups > 0;
        private int _pendingOnlineMeteorImpacts;
        private int _pendingOnlineMeteorGroups;
        public bool IsMeteorOnlinePresentationPending => _pendingOnlineMeteorImpacts > 0 || _pendingOnlineMeteorGroups > 0;

        public void CancelCombatPresentation()
        {
            _battleScreenPresenter?.CancelTemporaryReplicateExpiry();
            _battleScreenPresenter?.TutorialHand?.CancelSpellCardUses();
            _battleScreenPresenter?.TutorialHand?.CancelCardDraws();
            _lastLocalDrawBattleState = null;
            _lastLocalDrawEventCount = 0;
            _combatPresentationGeneration++;
            _pendingOnlineIronDomeImpacts = 0;
            _pendingOnlineIronDomeGroups = 0;
            _pendingOnlineReactiveArmourImpacts = 0;
            _pendingOnlineReactiveArmourGroups = 0;
            _pendingOnlineMeteorImpacts = 0;
            _pendingOnlineMeteorGroups = 0;
            _pendingOnlineFireSpearImpacts = 0;
            _pendingOnlineFireSpearGroups = 0;
            _pendingOnlinePlasmaShieldImpacts = 0;
            _pendingOnlinePlasmaShieldGroups = 0;
            _pendingOnlineManaShieldImpacts = 0;
            _pendingOnlineManaShieldGroups = 0;
            if (_blueDragonWaterCannon != null) _blueDragonWaterCannon.Cancel();
            if (_gwangmaCrossSlash != null) _gwangmaCrossSlash.Cancel();
            if (_redDragonFireBreath != null) _redDragonFireBreath.Cancel();
            if (_redDragonEndTurnFlamesVfx != null) _redDragonEndTurnFlamesVfx.Cancel();
            _pendingOnlineRedDragonImpacts = 0;
            _pendingOnlineRedDragonGroups = 0;
            if (_a212SawAttack != null) _a212SawAttack.Cancel();
            if (_a301LaserAttack != null) _a301LaserAttack.Cancel();
            if (_gaebangChiefCoinPunch != null) _gaebangChiefCoinPunch.Cancel();
            if (_manaCannonProjectile != null) _manaCannonProjectile.Cancel();
            if (_nuclearExplosionVfx != null) _nuclearExplosionVfx.Cancel();
            if (_timedBombPresentationVfx != null) _timedBombPresentationVfx.Cancel();
            if (_firewallVfx != null) _firewallVfx.Cancel();
            if (_earthquakeVfx != null) _earthquakeVfx.Cancel();
            if (_meteorVfx != null) _meteorVfx.Cancel();
            if (_fireSpearVfx != null) _fireSpearVfx.Cancel();
            if (_plasmaShieldVfx != null) _plasmaShieldVfx.Cancel();
            if (_ironDomeMissileVfx != null) _ironDomeMissileVfx.Cancel();
            if (_reactiveArmourVfx != null) _reactiveArmourVfx.Cancel();
            if (_manaShieldVfx != null) _manaShieldVfx.Cancel();
            _pendingOnlineSummonTrapImpacts = 0;
            _pendingOnlineSummonTrapGroups = 0;
            RestoreSummonTrapVisuals();
            var board = _battleScreenPresenter == null ? null : _battleScreenPresenter.BoardPresenter;
            if (board != null)
            {
                ResetTileCombatPresentation(board.PlayerTileViews);
                ResetTileCombatPresentation(board.AITileViews);
            }
        }

        private static void ResetTileCombatPresentation(TileView[] tiles)
        {
            if (tiles == null) return;
            foreach (var tile in tiles)
                if (tile != null) tile.GetComponent<TileTextView>()?.ResetCombatPresentation();
        }

        private void OnDisable() { CancelCombatPresentation(); ClearOpponentPlayedCardReveals(); }

        private BlueDragonWaterCannon GetBlueDragonWaterCannon()
        {
            if (_blueDragonWaterCannon == null)
                _blueDragonWaterCannon = gameObject.AddComponent<BlueDragonWaterCannon>();
            return _blueDragonWaterCannon;
        }
        private GwangmaCrossSlash GetGwangmaCrossSlash()
        {
            if (_gwangmaCrossSlash == null)
                _gwangmaCrossSlash = gameObject.AddComponent<GwangmaCrossSlash>();
            return _gwangmaCrossSlash;
        }
        private RedDragonFireBreath GetRedDragonFireBreath()
        {
            if (_redDragonFireBreath == null)
                _redDragonFireBreath = gameObject.AddComponent<RedDragonFireBreath>();
            return _redDragonFireBreath;
        }
        private A212SawAttack GetA212SawAttack()
        {
            if (_a212SawAttack == null)
                _a212SawAttack = gameObject.AddComponent<A212SawAttack>();
            return _a212SawAttack;
        }
        private A301LaserAttack GetA301LaserAttack()
        {
            if (_a301LaserAttack == null) _a301LaserAttack = gameObject.AddComponent<A301LaserAttack>();
            return _a301LaserAttack;
        }
        private ManaCannonProjectile GetManaCannonProjectile()
        {
            if (_manaCannonProjectile == null) _manaCannonProjectile = gameObject.AddComponent<ManaCannonProjectile>();
            return _manaCannonProjectile;
        }
        private GaebangChiefCoinPunch GetGaebangChiefCoinPunch()
        {
            if (_gaebangChiefCoinPunch == null) _gaebangChiefCoinPunch = gameObject.AddComponent<GaebangChiefCoinPunch>();
            return _gaebangChiefCoinPunch;
        }
        private float PlayIronDome(ImpactAnimationRequest request)
        {
            var cue = request.IronDomeCue;
            if (cue == null) return 0f;
            var master = FindTileTextView(cue.MasterOwnerId, cue.MasterCoord);
            var attacker = FindTileTextView(cue.AttackerOwnerId, cue.AttackerCoord);
            if (_ironDomeMissileVfx == null) _ironDomeMissileVfx = gameObject.AddComponent<IronDomeMissileVfx>();
            return _ironDomeMissileVfx.Play(master, cue.MasterRuntimeId, attacker, cue.AttackerRuntimeId);
        }

        private float PlayReactiveArmour(ImpactAnimationRequest request)
        {
            var cue = request.ReactiveArmourCue;
            if (cue == null) return 0f;
            var master = FindTileTextView(cue.MasterOwnerId, cue.MasterCoord);
            var attacker = FindTileTextView(cue.AttackerOwnerId, cue.AttackerCoord);
            if (_reactiveArmourVfx == null) _reactiveArmourVfx = gameObject.AddComponent<ReactiveArmourVfx>();
            return _reactiveArmourVfx.Play(master, cue.MasterRuntimeId, attacker, cue.AttackerRuntimeId);
        }

        private float PlayMeteor(PlayerId casterOwnerId)
        {
            if (_meteorVfx == null) _meteorVfx = gameObject.AddComponent<MeteorVfx>();
            return _meteorVfx.Play(_battleScreenPresenter?.BoardPresenter, casterOwnerId);
        }
        private float PlayEarthquake(PlayerId casterOwnerId)
        {
            if (_earthquakeVfx == null) _earthquakeVfx = gameObject.AddComponent<EarthquakeVfx>();
            return _earthquakeVfx.Play(_battleScreenPresenter?.BoardPresenter, casterOwnerId);
        }
        private float PlayNuclearExplosion(PlayerId casterOwnerId)
        {
            if (_nuclearExplosionVfx == null)
                _nuclearExplosionVfx = gameObject.AddComponent<NuclearExplosionVfx>();
            return _nuclearExplosionVfx.Play(_battleScreenPresenter?.BoardPresenter, casterOwnerId);
        }
        private float PlayTimedBombCountdown(TimedBombCountdownCue cue)
        {
            if (_timedBombPresentationVfx == null)
                _timedBombPresentationVfx = gameObject.AddComponent<TimedBombPresentationVfx>();
            return _timedBombPresentationVfx.Play(_battleScreenPresenter?.BoardPresenter,
                cue.Caster, cue.Before, cue.After);
        }
        private float PlayRedDragonEndTurnFlames(ImpactAnimationRequest request)
        {
            if (request.SpellEffectTargets.Count != BoardState.ColumnCount * BoardState.RowCount) return 0f;
            var owner = request.SpellEffectTargets[0].OwnerId;
            var seen = new bool[BoardState.ColumnCount * BoardState.RowCount];
            foreach (var target in request.SpellEffectTargets)
            {
                if (target.OwnerId != owner || target.Coord.Column < 0 || target.Coord.Column >= BoardState.ColumnCount ||
                    target.Coord.Row < 0 || target.Coord.Row >= BoardState.RowCount) return 0f;
                var index = target.Coord.Row * BoardState.ColumnCount + target.Coord.Column;
                if (seen[index]) return 0f;
                seen[index] = true;
            }
            if (_redDragonEndTurnFlamesVfx == null)
                _redDragonEndTurnFlamesVfx = gameObject.AddComponent<RedDragonEndTurnFlamesVfx>();
            return _redDragonEndTurnFlamesVfx.Play(_battleScreenPresenter?.BoardPresenter, owner);
        }

        private float PlayFirewall(ImpactAnimationRequest request)
        {
            if (request.SpellEffectTargets.Count != BoardState.ColumnCount) return 0f;
            var target = request.SpellEffectTargets[0];
            var columns = new bool[BoardState.ColumnCount];
            foreach (var tile in request.SpellEffectTargets)
            {
                if (tile.OwnerId != target.OwnerId || tile.Coord.Row != target.Coord.Row ||
                    tile.Coord.Column < 0 || tile.Coord.Column >= columns.Length || columns[tile.Coord.Column]) return 0f;
                columns[tile.Coord.Column] = true;
            }
            if (_firewallVfx == null) _firewallVfx = gameObject.AddComponent<FirewallVfx>();
            return _firewallVfx.Play(_battleScreenPresenter?.BoardPresenter, target.OwnerId, target.Coord.Row);
        }
        private static void ConfigureFirewallCast(ImpactAnimationRequest request, PlayerId targetOwnerId, TileCoord target)
        {
            request.SpellEffectId = FirewallSpellEffectId;
            request.SpellEffectTargets.Clear();
            for (var column = 0; column < BoardState.ColumnCount; column++)
                request.SpellEffectTargets.Add(new SpellEffectTarget(targetOwnerId, new TileCoord(column, target.Row)));
        }
        private bool IsFirewallCard(string cardId)
        {
            if (cardId == "Firewall") return true;
            if (string.IsNullOrEmpty(cardId) || _cardDefinitionProvider == null) return false;
            try { return _cardDefinitionProvider.GetRequired(cardId) is ScriptedSpellCardDefinition spell && spell.EffectId == FirewallSpellEffectId; }
            catch (InvalidOperationException) { return false; }
        }
        private float PlayFireSpear(ImpactAnimationRequest request)
        {
            if (request == null || !request.SpellCasterOwnerId.HasValue || request.SpellEffectTargets.Count != BoardState.RowCount)
                return 0f;
            var target = request.SpellEffectTargets[0];
            var rows = new bool[BoardState.RowCount];
            foreach (var tile in request.SpellEffectTargets)
            {
                if (tile.OwnerId != target.OwnerId || tile.Coord.Column != target.Coord.Column ||
                    tile.Coord.Row < 0 || tile.Coord.Row >= rows.Length || rows[tile.Coord.Row]) return 0f;
                rows[tile.Coord.Row] = true;
            }
            if (_fireSpearVfx == null) _fireSpearVfx = gameObject.AddComponent<FireSpearVfx>();
            return _fireSpearVfx.Play(_battleScreenPresenter?.BoardPresenter,
                request.SpellCasterOwnerId.Value, target.OwnerId, target.Coord.Column);
        }
        private static void ConfigureFireSpearCast(ImpactAnimationRequest request, PlayerId owner, TileCoord target)
        {
            request.SpellEffectId = FireSpearRules.EffectId;
            request.GuardAreaRuntimeIds = true;
            request.SpellEffectTargets.Clear();
            for (var row = 0; row < BoardState.RowCount; row++)
                request.SpellEffectTargets.Add(new SpellEffectTarget(owner, new TileCoord(target.Column, row)));
        }
        private float PlayPlasmaShield(ImpactAnimationRequest request)
        {
            if (request.SpellEffectTargets.Count != 1) return 0f;
            var target = request.SpellEffectTargets[0];
            var view = FindTileTextView(target.OwnerId, target.Coord);
            if (view == null) return 0f;
            if (_plasmaShieldVfx == null) _plasmaShieldVfx = gameObject.AddComponent<PlasmaShieldVfx>();
            return _plasmaShieldVfx.Play(view, request.SpellTargetRuntimeId);
        }

        private static void ConfigurePlasmaShieldCast(ImpactAnimationRequest request, PlayerId owner,
            TileCoord coord, string runtimeId)
        {
            request.SpellEffectId = PlasmaShieldRules.EffectId;
            request.SpellTargetRuntimeId = runtimeId;
            request.SpellEffectTargets.Clear();
            request.SpellEffectTargets.Add(new SpellEffectTarget(owner, coord));
        }

        private float PlayManaShield(ImpactAnimationRequest request)
        {
            if (request.SpellEffectTargets.Count != 1) return 0f;
            var target = request.SpellEffectTargets[0];
            var view = FindTileTextView(target.OwnerId, target.Coord);
            if (view == null) return 0f;
            if (_manaShieldVfx == null) _manaShieldVfx = gameObject.AddComponent<ManaShieldVfx>();
            return _manaShieldVfx.Play(view, request.SpellTargetRuntimeId);
        }

        private static void ConfigureManaShieldCast(ImpactAnimationRequest request, PlayerId owner,
            TileCoord coord, string runtimeId)
        {
            request.SpellEffectId = ManaShieldRules.EffectId;
            request.SpellTargetRuntimeId = runtimeId;
            request.SpellEffectTargets.Clear();
            request.SpellEffectTargets.Add(new SpellEffectTarget(owner, coord));
        }

        private bool IsFireSpearCard(string cardId)
        {
            if (cardId == FireSpearRules.CardId) return true;
            if (string.IsNullOrEmpty(cardId) || _cardDefinitionProvider == null) return false;
            try { return _cardDefinitionProvider.GetRequired(cardId) is ScriptedSpellCardDefinition spell && spell.EffectId == FireSpearRules.EffectId; }
            catch (InvalidOperationException) { return false; }
        }
        private float GetMeleeApproachDuration(string cardId)
        {
            if (cardId == GwangmaPresentationAssets.CardId) return Mathf.Max(_attackRunDuration, GwangmaPresentationAssets.ApproachDuration);
            if (cardId == RedDragonPresentationAssets.CardId) return Mathf.Max(_attackRunDuration, RedDragonPresentationAssets.ApproachDuration);
            if (cardId == A212PresentationAssets.CardId) return Mathf.Max(_attackRunDuration, A212PresentationAssets.ApproachDuration);
            if (cardId == GaebangChiefPresentationAssets.CardId) return Mathf.Max(_attackRunDuration, GaebangChiefPresentationAssets.ApproachDuration);
            return _attackRunDuration;
        }
        private float GetMeleeReturnDuration(string cardId)
        {
            if (cardId == GwangmaPresentationAssets.CardId) return Mathf.Max(_attackReturnDuration, GwangmaPresentationAssets.ReturnDuration);
            if (cardId == RedDragonPresentationAssets.CardId) return Mathf.Max(_attackReturnDuration, RedDragonPresentationAssets.ReturnDuration);
            if (cardId == A212PresentationAssets.CardId) return Mathf.Max(_attackReturnDuration, A212PresentationAssets.ReturnDuration);
            if (cardId == GaebangChiefPresentationAssets.CardId) return Mathf.Max(_attackReturnDuration, GaebangChiefPresentationAssets.ReturnDuration);
            return _attackReturnDuration;
        }
        private Coroutine _onlineErrorToastCoroutine;
        private Coroutine _onlineReconnectCountdownCoroutine;
        private Coroutine _onlineTurnTimerCoroutine;
        private Coroutine _yourTurnBannerCoroutine;
        private bool _hasOpponentReconnectCountdown;
        private bool _isLocalConnectionRecoveryStatusVisible;
        private float _opponentReconnectCountdownEndTime;
        [SerializeField, HideInInspector] private CanvasGroup _onlineErrorToastCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _onlineErrorToastPanelRect;
        [SerializeField, HideInInspector] private RectTransform _onlineErrorToastTextRect;
        [SerializeField, HideInInspector] private Image _onlineErrorToastPanelImage;
        [SerializeField, HideInInspector] private Text _onlineErrorToastText;
        [SerializeField, HideInInspector] private bool _onlineErrorToastUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _onlineTurnTimerCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _onlineTurnTimerPanelRect;
        [SerializeField, HideInInspector] private RectTransform _onlineTurnTimerTextRect;
        [SerializeField, HideInInspector] private Image _onlineTurnTimerPanelImage;
        [SerializeField, HideInInspector] private Text _onlineTurnTimerText;
        [SerializeField, HideInInspector] private bool _onlineTurnTimerUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _yourTurnBannerCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _yourTurnBannerRect;
        [SerializeField, HideInInspector] private Image _yourTurnBannerImage;
        [SerializeField, HideInInspector] private bool _yourTurnBannerUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _opponentPlayedCardRevealCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _opponentPlayedCardRevealRect;
        [SerializeField, HideInInspector] private Image _opponentPlayedCardRevealImage;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardFallbackText;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardAttackText;
        [SerializeField, HideInInspector] private Text _opponentPlayedCardHpText;
        [SerializeField, HideInInspector] private bool _opponentPlayedCardRevealUsesSceneLayout;
        [SerializeField, HideInInspector] private CanvasGroup _pvpMatchmakingCanvasGroup;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingDimRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingDimImage;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingPanelRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingPanelImage;
        [SerializeField, HideInInspector] private Outline _pvpMatchmakingPanelOutline;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingTextRect;
        [SerializeField, HideInInspector] private Text _pvpMatchmakingText;
        [SerializeField, HideInInspector] private RectTransform _pvpMatchmakingCancelButtonRect;
        [SerializeField, HideInInspector] private Image _pvpMatchmakingCancelButtonImage;
        [SerializeField, HideInInspector] private Button _pvpMatchmakingCancelButton;
        [SerializeField, HideInInspector] private Text _pvpMatchmakingCancelButtonText;
        [SerializeField, HideInInspector] private bool _pvpMatchmakingUsesSceneLayout;
#if UNITY_EDITOR
        private bool _hasQueuedEditorBattleUiMaterialization;
#endif
        private static Font s_runtimeKoreanToastFont;
        private bool _tutorialBattleEntry;
        private readonly RandomFirstPlayerSelector _firstPlayerSelector = new RandomFirstPlayerSelector();
        private readonly List<string> _combatLogEntries = new List<string>();
        private Text _opponentPlayedCardTrapTitleText;
        private readonly Queue<OpponentPlayedCardRevealRequest> _opponentPlayedCardRevealQueue =
            new Queue<OpponentPlayedCardRevealRequest>();
        private Coroutine _opponentPlayedCardRevealCoroutine;
        private IReadOnlyList<string> _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
        private IReadOnlyList<string> _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
        private bool _isBusy;
        private BattleState _endTurnSubmittedBattle;
        private int _endTurnSubmittedNumber = -1;
        private bool _hasQueuedDraftSceneReturn;
        private bool _isPvpMatchmakingOverlayVisible;
        private bool _isCancellingPvpMatchmaking;

        public BattleState CurrentBattleState => GetCurrentBattleState();

        public string CombatLogText => _combatLogText;

        public string LastInteractionStatus => _lastInteractionStatus;

        // Card reveals stay independent of action input, but must finish before the result/scene return.
        public bool IsOpponentPlayedCardRevealPending =>
            _opponentPlayedCardRevealCoroutine != null || _opponentPlayedCardRevealQueue.Count > 0;

        public bool IsBusy => _tutorialResolving || _isBusy || IsEndTurnInputLocked || IsRedDragonOnlinePresentationPending || IsMeteorOnlinePresentationPending || IsFireSpearOnlinePresentationPending || IsPlasmaShieldOnlinePresentationPending || IsManaShieldOnlinePresentationPending || IsReactiveArmourOnlinePresentationPending || IsIronDomeOnlinePresentationPending || IsSummonTrapOnlinePresentationPending;

        public bool CanPlayerEndTurn => TutorialAllowsEndTurn && !IsBusy && CurrentBattleState != null &&
            !CurrentBattleState.IsEnded && CurrentBattleState.Phase == PhaseType.Main &&
            CurrentBattleState.ActivePlayerId == PlayerId.Player && CurrentBattleState.PendingRobotFusion == null;

        private bool IsEndTurnInputLocked
        {
            get
            {
                if (_sendPlayerActionsToOnlineGateway)
                    return _onlineConnectionTester != null && _onlineConnectionTester.IsAwaitingNextTurn;
                var state = CurrentBattleState;
                if (state == null || state != _endTurnSubmittedBattle || state.IsEnded) return false;
                return !(state.TurnNumber > _endTurnSubmittedNumber && state.ActivePlayerId == PlayerId.Player &&
                    state.Phase == PhaseType.Main);
            }
        }

        public bool IsPvpMatchmakingOverlayVisible => _isPvpMatchmakingOverlayVisible;

        public bool CanPlayerSurrender
        {
            get
            {
                var battleState = CurrentBattleState;
                if (battleState == null || battleState.IsEnded)
                {
                    return false;
                }

                if (!_sendPlayerActionsToOnlineGateway)
                {
                    return true;
                }

                return _onlineConnectionTester != null &&
                       _onlineConnectionTester.HasActiveAuthoritativeBattle;
            }
        }

        public bool CanSendPlayerSurrender =>
            CanPlayerSurrender &&
            (!_sendPlayerActionsToOnlineGateway ||
             (_onlineConnectionTester != null && _onlineConnectionTester.IsReadyForCommands));

        public bool IsCancellingPvpMatchmaking => _isCancellingPvpMatchmaking;

        public bool IsMulliganPresentationActive =>
            _battleScreenPresenter != null && _battleScreenPresenter.IsMulliganPresentationActive;

        public void ShowOnlineErrorToast(string message)
        {
            ShowOnlineToast(message, _onlineErrorToastPanelColor);
        }

        public void ShowOnlineStatusToast(string message)
        {
            ShowOnlineToast(message, _onlineStatusToastPanelColor);
        }

        public void ShowLocalConnectionRecoveryStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
                _onlineErrorToastCoroutine = null;
            }

            _isLocalConnectionRecoveryStatusVisible = true;
            _lastInteractionStatus = message;
            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = NoticeTheme.PanelColor;
            }

            _onlineErrorToastText.text = message;
            NoticeTheme.Toast(_onlineErrorToastPanelRect, _onlineErrorToastText);
            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.interactable = false;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
        }

        public void HideLocalConnectionRecoveryStatus()
        {
            if (!_isLocalConnectionRecoveryStatusVisible)
            {
                return;
            }

            _isLocalConnectionRecoveryStatusVisible = false;
            if (TryResumeOpponentReconnectCountdown())
            {
                return;
            }

            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.interactable = false;
                _onlineErrorToastCanvasGroup.blocksRaycasts = false;
                _onlineErrorToastCanvasGroup.gameObject.SetActive(false);
            }
        }

        public void ShowOnlineSuccessToast(string message)
        {
            ShowOnlineToast(message, _onlineSuccessToastPanelColor);
        }

        public void ShowBattleResultToast(string message)
        {
            ShowOnlineToast(message, _onlineSuccessToastPanelColor);
        }

        public void CancelPendingSpellCardUse() => _battleScreenPresenter?.TutorialHand?.CancelPendingSpellCardUses();

        public void ShowCommandErrorToast(string message)
        {
            ShowOnlineToast(message, _onlineErrorToastPanelColor);
        }

        public void ShowOpponentReconnectCountdown(int durationSeconds)
        {
            _isLocalConnectionRecoveryStatusVisible = false;
            var safeDurationSeconds = Mathf.Max(0, durationSeconds);
            _hasOpponentReconnectCountdown = true;
            _opponentReconnectCountdownEndTime = Time.unscaledTime + safeDurationSeconds;
            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
                _onlineErrorToastCoroutine = null;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = NoticeTheme.PanelColor;
            }

            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            _onlineReconnectCountdownCoroutine = StartCoroutine(OpponentReconnectCountdownSequence());
        }

        public void HideOpponentReconnectCountdown()
        {
            _hasOpponentReconnectCountdown = false;
            _opponentReconnectCountdownEndTime = 0f;
            StopOnlineReconnectCountdown();
            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            }
        }

        public void ShowOnlineTurnTimer(bool isLocalPlayerTurn, float remainingSeconds, int durationSeconds)
        {
            ShowOnlinePhaseTimer(
                isMulligan: false,
                localMulliganConfirmed: false,
                isLocalPlayerTurn: isLocalPlayerTurn,
                remainingSeconds: remainingSeconds,
                durationSeconds: durationSeconds);
        }

        public void ShowOnlineMulliganTimer(bool localMulliganConfirmed, float remainingSeconds, int durationSeconds)
        {
            ShowOnlinePhaseTimer(
                isMulligan: true,
                localMulliganConfirmed: localMulliganConfirmed,
                isLocalPlayerTurn: true,
                remainingSeconds: remainingSeconds,
                durationSeconds: durationSeconds);
        }

        private void ShowOnlinePhaseTimer(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            float remainingSeconds,
            int durationSeconds)
        {
            var safeRemainingSeconds = Mathf.Max(0f, remainingSeconds);
            var safeDurationSeconds = Mathf.Max(1, durationSeconds);
            StopOnlineTurnTimer();
            EnsureOnlineTurnTimer();
            if (_onlineTurnTimerCanvasGroup == null || _onlineTurnTimerText == null)
            {
                return;
            }

            _onlineTurnTimerCanvasGroup.gameObject.SetActive(true);
            _onlineTurnTimerCanvasGroup.alpha = 1f;
            _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
            _onlineTurnTimerCoroutine = StartCoroutine(OnlineTurnTimerSequence(
                isMulligan,
                localMulliganConfirmed,
                isLocalPlayerTurn,
                safeRemainingSeconds,
                safeDurationSeconds));
        }

        public void HideOnlineTurnTimer()
        {
            StopOnlineTurnTimer();
            if (_onlineTurnTimerCanvasGroup != null)
            {
                _onlineTurnTimerCanvasGroup.alpha = 0f;
                _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
                _onlineTurnTimerCanvasGroup.gameObject.SetActive(false);
            }
        }

        public void ShowPvpMatchmakingOverlay()
        {
            EnsurePvpMatchmakingOverlay();
            if (_pvpMatchmakingCanvasGroup == null || _pvpMatchmakingText == null)
            {
                return;
            }

            ApplyPvpMatchmakingOverlaySettings();
            _isPvpMatchmakingOverlayVisible = true;
            _isCancellingPvpMatchmaking = false;
            _pvpMatchmakingText.text = _pvpMatchmakingWaitingText;
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingCancelButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = true;
            }

            _pvpMatchmakingCanvasGroup.gameObject.SetActive(true);
            _pvpMatchmakingCanvasGroup.alpha = 1f;
            _pvpMatchmakingCanvasGroup.interactable = true;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = true;
        }

        public void HidePvpMatchmakingOverlay(bool showFoundMessage = false)
        {
            if (!_isPvpMatchmakingOverlayVisible && _pvpMatchmakingCanvasGroup == null)
            {
                return;
            }

            if (showFoundMessage)
            {
                ShowOnlineSuccessToast(_pvpMatchmakingFoundText);
            }

            _isPvpMatchmakingOverlayVisible = false;
            _isCancellingPvpMatchmaking = false;
            if (_pvpMatchmakingCanvasGroup == null)
            {
                return;
            }

            _pvpMatchmakingCanvasGroup.alpha = 0f;
            _pvpMatchmakingCanvasGroup.interactable = false;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = false;
            _pvpMatchmakingCanvasGroup.gameObject.SetActive(false);
        }

        public void ShowPvpMatchmakingConnectionFailure(string message = null)
        {
            EnsurePvpMatchmakingOverlay();
            if (_pvpMatchmakingCanvasGroup == null || _pvpMatchmakingText == null)
            {
                return;
            }

            ApplyPvpMatchmakingOverlaySettings();
            _isPvpMatchmakingOverlayVisible = true;
            _isCancellingPvpMatchmaking = false;
            _pvpMatchmakingText.text = string.IsNullOrWhiteSpace(message)
                ? _pvpMatchmakingConnectionFailedText
                : message;
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingReturnButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = true;
            }

            _pvpMatchmakingCanvasGroup.gameObject.SetActive(true);
            _pvpMatchmakingCanvasGroup.alpha = 1f;
            _pvpMatchmakingCanvasGroup.interactable = true;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = true;
        }

        public void SetPvpMatchmakingOverlayMessage(string message)
        {
            if (!_isPvpMatchmakingOverlayVisible || _pvpMatchmakingText == null)
            {
                return;
            }

            _pvpMatchmakingText.text = string.IsNullOrWhiteSpace(message)
                ? _pvpMatchmakingWaitingText
                : message;
        }

        public void ReturnFromUnavailableOnlineBattle()
        {
            // Bypass ReturnToDraftSceneAfterDelay: there is no local battle result to record.
            DraftRunSessionState.PrepareForUnavailableBattleReturn();
            SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
        }
        public async void CancelPvpMatchmakingFromUi()
        {
            if (!_isPvpMatchmakingOverlayVisible || _isCancellingPvpMatchmaking)
            {
                return;
            }

            _isCancellingPvpMatchmaking = true;
            _lastInteractionStatus = "PvP matchmaking cancelled.";
            SetPvpMatchmakingOverlayMessage("매칭을 취소하는 중입니다...");
            SetPvpMatchmakingCancelButtonLabel(_pvpMatchmakingCancelButtonLabel);
            if (_pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.interactable = false;
            }

            if (_onlineConnectionTester != null)
            {
                await _onlineConnectionTester.DisconnectFromServerAsync();
            }

            HidePvpMatchmakingOverlay();
            DraftRunSessionState.RestoreCurrentDraftDeckFromCompletedDeckIfNeeded();

            if (!string.IsNullOrWhiteSpace(DraftRunSessionState.DraftSceneName))
            {
                SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
            }
        }

        private void SetPvpMatchmakingCancelButtonLabel(string label)
        {
            if (_pvpMatchmakingCancelButtonText != null)
            {
                _pvpMatchmakingCancelButtonText.text = string.IsNullOrWhiteSpace(label)
                    ? _pvpMatchmakingCancelButtonLabel
                    : label;
            }
        }

        private void ShowOnlineToast(string message, Color panelColor)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var shouldResumeReconnectCountdown = HasActiveOpponentReconnectCountdown();
            _isLocalConnectionRecoveryStatusVisible = false;
            StopOnlineReconnectCountdown();
            var displayMessage = message;

            _lastInteractionStatus = displayMessage;
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = NoticeTheme.PanelColor;
            }

            // The borrowed scene's toast must remain above the tutorial dialogue canvas.
            if (_tutorialBattleEntry && _onlineErrorToastCanvasGroup != null)
                _onlineErrorToastCanvasGroup.GetComponent<Canvas>().sortingOrder = short.MaxValue - 1;
            _onlineErrorToastText.text = displayMessage;
            NoticeTheme.Toast(_onlineErrorToastPanelRect, _onlineErrorToastText);
            if (_onlineErrorToastCoroutine != null)
            {
                StopCoroutine(_onlineErrorToastCoroutine);
            }

            _onlineErrorToastCoroutine = StartCoroutine(OnlineErrorToastSequence(shouldResumeReconnectCountdown));
        }

        private void StopOnlineReconnectCountdown()
        {
            if (_onlineReconnectCountdownCoroutine == null)
            {
                return;
            }

            StopCoroutine(_onlineReconnectCountdownCoroutine);
            _onlineReconnectCountdownCoroutine = null;
        }

        private bool HasActiveOpponentReconnectCountdown()
        {
            return _hasOpponentReconnectCountdown &&
                   _opponentReconnectCountdownEndTime > Time.unscaledTime;
        }

        private bool TryResumeOpponentReconnectCountdown()
        {
            if (!HasActiveOpponentReconnectCountdown())
            {
                return false;
            }

            StopOnlineReconnectCountdown();
            EnsureOnlineErrorToast();
            if (_onlineErrorToastCanvasGroup == null || _onlineErrorToastText == null)
            {
                return false;
            }

            ApplyOnlineErrorToastSettings();
            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = NoticeTheme.PanelColor;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 1f;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;
            _onlineReconnectCountdownCoroutine = StartCoroutine(OpponentReconnectCountdownSequence());
            return true;
        }

        private IEnumerator OpponentReconnectCountdownSequence()
        {
            while (true)
            {
                var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(_opponentReconnectCountdownEndTime - Time.unscaledTime));
                var message = $"상대의 재접속을 기다리는 중...{remainingSeconds}";
                _lastInteractionStatus = message;
                if (_onlineErrorToastText != null)
                {
                    _onlineErrorToastText.text = message;
            NoticeTheme.Toast(_onlineErrorToastPanelRect, _onlineErrorToastText);
                }

                if (remainingSeconds <= 0)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            _onlineReconnectCountdownCoroutine = null;
            _hasOpponentReconnectCountdown = false;
        }

        private void StopOnlineTurnTimer()
        {
            if (_onlineTurnTimerCoroutine == null)
            {
                return;
            }

            StopCoroutine(_onlineTurnTimerCoroutine);
            _onlineTurnTimerCoroutine = null;
        }

        private IEnumerator OnlineTurnTimerSequence(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            float remainingSeconds,
            int durationSeconds)
        {
            var endTime = Time.unscaledTime + Mathf.Max(0f, remainingSeconds);
            while (true)
            {
                var remainingWholeSeconds = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.unscaledTime));
                ApplyOnlineTurnTimerValue(
                    isMulligan,
                    localMulliganConfirmed,
                    isLocalPlayerTurn,
                    remainingWholeSeconds,
                    durationSeconds);

                if (remainingWholeSeconds <= 0)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            _onlineTurnTimerCoroutine = null;
        }

        private void ApplyOnlineTurnTimerValue(
            bool isMulligan,
            bool localMulliganConfirmed,
            bool isLocalPlayerTurn,
            int remainingSeconds,
            int durationSeconds)
        {
            ApplyOnlineTurnTimerSettings();

            var shouldReveal = isMulligan ||
                               remainingSeconds <= Mathf.Max(0, _onlineTurnTimerRevealThresholdSeconds);
            if (_onlineTurnTimerCanvasGroup != null)
            {
                _onlineTurnTimerCanvasGroup.alpha = shouldReveal ? 1f : 0f;
                _onlineTurnTimerCanvasGroup.blocksRaycasts = false;
            }

            var isDanger = remainingSeconds <= Mathf.Max(0, _onlineTurnTimerDangerThresholdSeconds);
            if (_onlineTurnTimerPanelImage != null)
            {
                _onlineTurnTimerPanelImage.color = isDanger
                    ? _onlineTurnTimerDangerPanelColor
                    : isMulligan && localMulliganConfirmed
                        ? _onlineTurnTimerOpponentPanelColor
                        : isLocalPlayerTurn
                        ? _onlineTurnTimerPlayerPanelColor
                        : _onlineTurnTimerOpponentPanelColor;
            }

            if (_onlineTurnTimerText != null)
            {
                if (isMulligan)
                {
                    _onlineTurnTimerText.text = remainingSeconds <= 0
                        ? "멀리건 처리 중..."
                        : localMulliganConfirmed
                            ? $"상대 멀리건 대기 중... {remainingSeconds}"
                            : $"멀리건 {remainingSeconds}";
                }
                else
                {
                    var turnLabel = isLocalPlayerTurn ? "내 턴" : "상대 턴";
                    _onlineTurnTimerText.text = remainingSeconds <= 0
                        ? $"{turnLabel} 처리 중..."
                        : $"{turnLabel} {remainingSeconds}";
                }
                _onlineTurnTimerText.color = isDanger ? _onlineTurnTimerDangerTextColor : _onlineTurnTimerTextColor;
            }
            NoticeTheme.Inline(_onlineTurnTimerPanelRect, _onlineTurnTimerText);
        }

        private IEnumerator ShowYourTurnBannerAfterCountdowns(float delay)
        {
            var generation = _combatPresentationGeneration;
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            if (generation == _combatPresentationGeneration && isActiveAndEnabled && CurrentBattleState?.IsEnded != true)
                ShowYourTurnBanner();
        }

        private void ShowYourTurnBanner()
        {
            EnsureYourTurnBanner();
            ApplyYourTurnBannerSettings();
            if (_yourTurnBannerCanvasGroup == null ||
                _yourTurnBannerImage == null ||
                _yourTurnBannerImage.sprite == null)
            {
                return;
            }

            if (_yourTurnBannerCoroutine != null)
            {
                StopCoroutine(_yourTurnBannerCoroutine);
            }

            _yourTurnBannerCoroutine = StartCoroutine(YourTurnBannerSequence());
        }

        private void HideYourTurnBanner()
        {
            if (_yourTurnBannerCoroutine != null)
            {
                StopCoroutine(_yourTurnBannerCoroutine);
                _yourTurnBannerCoroutine = null;
            }

            if (_yourTurnBannerCanvasGroup == null)
            {
                return;
            }

            _yourTurnBannerCanvasGroup.alpha = 0f;
            _yourTurnBannerCanvasGroup.interactable = false;
            _yourTurnBannerCanvasGroup.blocksRaycasts = false;
            _yourTurnBannerCanvasGroup.gameObject.SetActive(false);
        }

        private IEnumerator YourTurnBannerSequence()
        {
            while (_battleScreenPresenter != null && _battleScreenPresenter.IsMulliganPresentationActive)
            {
                yield return null;
            }

            if (_yourTurnBannerCanvasGroup == null)
            {
                _yourTurnBannerCoroutine = null;
                yield break;
            }

            ApplyYourTurnBannerSettings();
            _yourTurnBannerCanvasGroup.gameObject.SetActive(true);
            _yourTurnBannerCanvasGroup.alpha = 0f;
            _yourTurnBannerCanvasGroup.interactable = false;
            _yourTurnBannerCanvasGroup.blocksRaycasts = false;

            yield return FadeYourTurnBanner(0f, 1f, _yourTurnBannerFadeInSeconds);
            if (_yourTurnBannerVisibleSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(_yourTurnBannerVisibleSeconds);
            }

            yield return FadeYourTurnBanner(1f, 0f, _yourTurnBannerFadeOutSeconds);

            if (_yourTurnBannerCanvasGroup != null)
            {
                _yourTurnBannerCanvasGroup.alpha = 0f;
                _yourTurnBannerCanvasGroup.gameObject.SetActive(false);
            }

            _yourTurnBannerCoroutine = null;
        }

        private IEnumerator FadeYourTurnBanner(float fromAlpha, float toAlpha, float durationSeconds)
        {
            if (_yourTurnBannerCanvasGroup == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _yourTurnBannerCanvasGroup.alpha = toAlpha;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / durationSeconds);
                _yourTurnBannerCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                yield return null;
            }

            _yourTurnBannerCanvasGroup.alpha = toAlpha;
        }

        public float PlayOnlineSpellCastAnimation(string cardId, PlayerId targetOwnerId, TileCoord targetCoord, int damageAmount)
        {
            var spellEffectId = ResolveDamageSpellEffectId(cardId);
            if (string.IsNullOrWhiteSpace(spellEffectId))
            {
                return 0f;
            }

            var targetView = FindTileTextView(targetOwnerId, targetCoord);
            if (targetView == null)
            {
                return 0f;
            }

            var effectFrames = GetSpellEffectFrames(spellEffectId);
            if (effectFrames == null || effectFrames.Count == 0)
            {
                return 0f;
            }

            var duration = targetView.PlayTransientSpriteEffect(
                effectFrames,
                GetSpellEffectFramesPerSecond(spellEffectId),
                GetSpellEffectSize(spellEffectId));

            if (damageAmount > 0)
            {
                targetView.QueueFloatingValuePopup(damageAmount, isHealing: false, delaySeconds: 0.08f);
            }

            return duration;
        }

        public void QueueOpponentPlayedCardReveals(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null) return;
            foreach (var battleEvent in battleEvents)
            {
                if (!OpponentPlayedCardRevealRules.ShouldReveal(battleEvent)) continue;
                // Even a malformed installation payload cannot reach artwork/name/stat lookup.
                var hidden = battleEvent.EventType == BattleEventType.TrapInstalled;
                _opponentPlayedCardRevealQueue.Enqueue(hidden
                    ? new OpponentPlayedCardRevealRequest(string.Empty, 0, 0, null, isHiddenTrap: true)
                    : new OpponentPlayedCardRevealRequest(battleEvent.CardId, battleEvent.CardAttack,
                        battleEvent.CardMaxHp, battleEvent.CardSpellDamage));
            }
            StartOpponentPlayedCardRevealQueue();
        }

        private void QueueLocalTrapReveals()
        {
            // Projected online states restore counts only; their history is delivered by the online event queue.
            if (_sendPlayerActionsToOnlineGateway) return;
            var battle = _battleGateway?.CurrentBattleState;
            if (battle == null || battle.TrapEvents.Count == 0) return;
            var events = new List<BattleTrapEvent>(battle.TrapEvents);
            battle.ClearTrapEvents();
            foreach (var trapEvent in events)
            {
                if (trapEvent.Kind == BattleTrapEventKind.Installed)
                {
                    if (trapEvent.OwnerId == PlayerId.AI)
                        _opponentPlayedCardRevealQueue.Enqueue(new OpponentPlayedCardRevealRequest(string.Empty, 0, 0, null, isHiddenTrap: true));
                }
                else if (trapEvent.Kind == BattleTrapEventKind.Triggered && !string.IsNullOrWhiteSpace(trapEvent.CardId))
                    _opponentPlayedCardRevealQueue.Enqueue(new OpponentPlayedCardRevealRequest(trapEvent.CardId, 0, 0,
                        trapEvent.CardId == SummonTrapCardRules.CardId ? (int?)trapEvent.EffectAmount : null));
            }
            StartOpponentPlayedCardRevealQueue();
        }

        private void StartOpponentPlayedCardRevealQueue()
        {
            if (_opponentPlayedCardRevealQueue.Count <= 0 || _opponentPlayedCardRevealCoroutine != null) return;
            EnsureOpponentPlayedCardReveal();
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                _opponentPlayedCardRevealQueue.Clear();
                return;
            }
            _opponentPlayedCardRevealCoroutine = StartCoroutine(OpponentPlayedCardRevealSequence());
        }
        public void ClearOpponentPlayedCardReveals()
        {
            _opponentPlayedCardRevealQueue.Clear();
            if (_opponentPlayedCardRevealCoroutine != null)
            {
                StopCoroutine(_opponentPlayedCardRevealCoroutine);
                _opponentPlayedCardRevealCoroutine = null;
            }

            HideOpponentPlayedCardReveal();
        }

        private IEnumerator OpponentPlayedCardRevealSequence()
        {
            while (_opponentPlayedCardRevealQueue.Count > 0)
            {
                var request = _opponentPlayedCardRevealQueue.Dequeue();
                PresentOpponentPlayedCardReveal(request);

                var sequenceStartedAt = Time.unscaledTime;
                yield return FadeOpponentPlayedCardReveal(
                    0f,
                    1f,
                    Mathf.Max(0f, _opponentPlayedCardFadeInSeconds));

                while (true)
                {
                    RefreshOpponentPlayedCardStatLayout();
                    var displayDuration = OpponentPlayedCardRevealRules.ResolveDisplayDuration(
                        _opponentPlayedCardRevealQueue.Count > 0,
                        _opponentPlayedCardNormalDisplaySeconds,
                        _opponentPlayedCardQueuedDisplaySeconds);
                    var fadeOutStart = Mathf.Max(
                        _opponentPlayedCardFadeInSeconds,
                        displayDuration - _opponentPlayedCardFadeOutSeconds);
                    if (Time.unscaledTime - sequenceStartedAt >= fadeOutStart)
                    {
                        break;
                    }

                    yield return null;
                }

                yield return FadeOpponentPlayedCardReveal(
                    _opponentPlayedCardRevealCanvasGroup == null
                        ? 0f
                        : _opponentPlayedCardRevealCanvasGroup.alpha,
                    0f,
                    Mathf.Max(0f, _opponentPlayedCardFadeOutSeconds));
                HideOpponentPlayedCardReveal();
            }

            _opponentPlayedCardRevealCoroutine = null;
        }

        private IEnumerator FadeOpponentPlayedCardReveal(float fromAlpha, float toAlpha, float durationSeconds)
        {
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _opponentPlayedCardRevealCanvasGroup.alpha = toAlpha;
                yield break;
            }

            var startedAt = Time.unscaledTime;
            while (true)
            {
                RefreshOpponentPlayedCardStatLayout();
                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / durationSeconds);
                _opponentPlayedCardRevealCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                if (progress >= 1f)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private void PresentOpponentPlayedCardReveal(OpponentPlayedCardRevealRequest request)
        {
            EnsureOpponentPlayedCardReveal();
            if (_opponentPlayedCardRevealCanvasGroup == null || _opponentPlayedCardRevealImage == null)
            {
                return;
            }

            Sprite artwork;
            var hasArtwork = request.IsHiddenTrap
                ? (artwork = TrapPresentationAssets.HiddenArtwork) != null
                : CardArtworkLibrary.TryGetArtwork(request.CardId, out artwork) && artwork != null;
            SetTrapRevealTitle(request.IsHiddenTrap);
            _opponentPlayedCardRevealImage.sprite = hasArtwork ? artwork : null;
            _opponentPlayedCardRevealImage.color = hasArtwork
                ? Color.white
                : new Color(0.035f, 0.055f, 0.085f, 0.96f);
            _opponentPlayedCardRevealImage.enabled = true;

            if (_opponentPlayedCardFallbackText != null)
            {
                _opponentPlayedCardFallbackText.text = request.IsHiddenTrap ? "?" : request.CardId;
                _opponentPlayedCardFallbackText.gameObject.SetActive(!hasArtwork);
            }

            var showHp = !request.IsHiddenTrap && _showOpponentPlayedCardStats && request.CardMaxHp > 0;
            var showAttack = !request.IsHiddenTrap && (showHp || (_showOpponentPlayedCardStats && request.CardSpellDamage.HasValue));
            if (_opponentPlayedCardAttackText != null)
            {
                _opponentPlayedCardAttackText.text = (request.CardSpellDamage ?? request.CardAttack).ToString();
                _opponentPlayedCardAttackText.gameObject.SetActive(showAttack);
            }

            if (_opponentPlayedCardHpText != null)
            {
                _opponentPlayedCardHpText.text = request.CardMaxHp.ToString();
                _opponentPlayedCardHpText.gameObject.SetActive(showHp);
            }

            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            _opponentPlayedCardRevealCanvasGroup.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RefreshOpponentPlayedCardStatLayout();
        }

        private void SetTrapRevealTitle(bool visible)
        {
            if (!visible) { if (_opponentPlayedCardTrapTitleText != null) _opponentPlayedCardTrapTitleText.gameObject.SetActive(false); return; }
            if (_opponentPlayedCardTrapTitleText == null)
            {
                _opponentPlayedCardTrapTitleText = CreateOpponentPlayedCardStatText(_opponentPlayedCardRevealRect, "TrapTitleText");
                var rect = _opponentPlayedCardTrapTitleText.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.offsetMin = new Vector2(12f, -70f);
                rect.offsetMax = new Vector2(-12f, -10f);
                _opponentPlayedCardTrapTitleText.font = ResolveOnlineErrorToastFont();
                _opponentPlayedCardTrapTitleText.fontSize = 30;
                _opponentPlayedCardTrapTitleText.color = new Color(1f, .83f, .34f, 1f);
                var outline = _opponentPlayedCardTrapTitleText.GetComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            _opponentPlayedCardTrapTitleText.text = "트랩";
            _opponentPlayedCardTrapTitleText.gameObject.SetActive(true);
        }

        private void RefreshOpponentPlayedCardStatLayout()
        {
            if (_opponentPlayedCardRevealImage == null ||
                _opponentPlayedCardRevealImage.sprite == null)
            {
                return;
            }

            var artworkRectTransform = _opponentPlayedCardRevealImage.rectTransform;
            var renderedSpriteRect = HandCardStatOverlayLayout.CalculateRenderedSpriteRect(_opponentPlayedCardRevealImage);
            PositionOpponentPlayedCardStatText(
                _opponentPlayedCardAttackText,
                artworkRectTransform,
                renderedSpriteRect,
                _opponentPlayedCardAttackStatNormalizedPosition);
            PositionOpponentPlayedCardStatText(
                _opponentPlayedCardHpText,
                artworkRectTransform,
                renderedSpriteRect,
                _opponentPlayedCardHpStatNormalizedPosition);
        }


        private static void PositionOpponentPlayedCardStatText(Text text, RectTransform artworkRectTransform,
            Rect renderedSpriteRect, Vector2 normalizedSpritePosition)
        {
            HandCardStatOverlayLayout.PositionStatText(text?.rectTransform, artworkRectTransform,
                renderedSpriteRect, normalizedSpritePosition);
        }

        private void HideOpponentPlayedCardReveal()
        {
            if (_opponentPlayedCardRevealCanvasGroup == null)
            {
                return;
            }

            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            _opponentPlayedCardRevealCanvasGroup.gameObject.SetActive(false);
        }

        public float PlayOnlineBattleEventAnimations(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return 0f;
            }

            var cardDrawCount = 0;
            var hasAcceptedUnitMove = false;
            var startsLocalPlayerTurn = false;
            for (var eventIndex = 0; eventIndex < battleEvents.Count; eventIndex++)
            {
                var battleEvent = battleEvents[eventIndex];
                if (battleEvent?.EventType == BattleEventType.OccupantMoved &&
                    battleEvent.SourceCoord != null && battleEvent.TargetCoord != null &&
                    (battleEvent.SourceOwnerId != battleEvent.TargetOwnerId ||
                     battleEvent.SourceCoord.Column != battleEvent.TargetCoord.Column ||
                     battleEvent.SourceCoord.Row != battleEvent.TargetCoord.Row))
                    hasAcceptedUnitMove = true;

                if (battleEvent?.EventType == BattleEventType.TrapInstalled &&
                    battleEvent.SourceOwnerId == PlayerId.Player)
                    _battleScreenPresenter?.TutorialHand?.ConfirmTrapCardUse();

                if (battleEvent?.EventType == BattleEventType.SpellCast &&
                    battleEvent.SourceOwnerId == PlayerId.Player)
                    _battleScreenPresenter?.TutorialHand?.ConfirmSpellCardUse(
                        string.IsNullOrEmpty(battleEvent.CardId) ? battleEvent.SourceCardId : battleEvent.CardId);

                if (battleEvent?.EventType == BattleEventType.CardDrawn &&
                    battleEvent.SourceOwnerId == PlayerId.Player)
                {
                    cardDrawCount += Mathf.Max(1, battleEvent.Amount);
                }

                if (battleEvent?.EventType == BattleEventType.TurnEnded &&
                    battleEvent.SourceOwnerId == PlayerId.Player)
                    _battleScreenPresenter?.QueueTemporaryReplicateExpiry();

                if (battleEvent?.EventType == BattleEventType.TurnStarted &&
                    battleEvent.SourceOwnerId == PlayerId.Player)
                {
                    startsLocalPlayerTurn = true;
                }
            }

            _battleScreenPresenter?.QueueCardDrawAnimations(cardDrawCount);
            _battleScreenPresenter?.QueueCardDrawSounds(cardDrawCount);
            // A swap contains two OccupantMoved events, but is one audible action.
            // This remains independent of the optional visual movement animation.
            if (hasAcceptedUnitMove) _battleScreenPresenter?.PlayUnitMoveSound();
            var hasTimedBombCountdown = FindFirstBattleEvent(battleEvents, BattleEventType.TimedBombCountdown) != null;
            var endsBattle = FindFirstBattleEvent(battleEvents, BattleEventType.BattleEnded) != null;
            if (startsLocalPlayerTurn && !hasTimedBombCountdown && !endsBattle)
            {
                ShowYourTurnBanner();
            }

            QueueOpponentPlayedCardReveals(battleEvents);

            var animationGroups = BuildOnlineAnimationGroups(battleEvents);
            if (startsLocalPlayerTurn && hasTimedBombCountdown && !endsBattle)
                StartCoroutine(ShowYourTurnBannerAfterCountdowns(EstimateOnlineBattleEventAnimationGroupsDuration(animationGroups)));
            if (animationGroups.Count == 0)
            {
                return 0f;
            }

            if (animationGroups.Count == 1)
            {
                return PlayOnlineBattleEventAnimationGroup(animationGroups[0]);
            }

            var groups = PlayOnlineBattleEventAnimationGroups(animationGroups);
            var containsRedDragon = false;
            var containsMeteor = false;
            var containsFireSpear = false;
            var containsPlasmaShield = false;
            var containsManaShield = false;
            var containsIronDome = false;
            var containsReactiveArmour = false;
            var containsSummonTrap = false;
            foreach (var group in animationGroups)
                foreach (var battleEvent in group)
                    if (battleEvent?.EventType == BattleEventType.AreaSpellEffectTriggered &&
                        battleEvent.EffectId == EndTurnService.RedDragonFlamesEffectId)
                        containsRedDragon = true;
                    else if (battleEvent?.EventType == BattleEventType.SpellCast)
                    {
                        if (battleEvent.CardId == MeteorRules.CardId) containsMeteor = true;
                        if (battleEvent.CardId == FireSpearRules.CardId) containsFireSpear = true;
                        if (battleEvent.CardId == PlasmaShieldRules.CardId) containsPlasmaShield = true;
                        if (battleEvent.CardId == ManaShieldRules.CardId) containsManaShield = true;
                    }
                    else if (battleEvent?.EventType == BattleEventType.TrapTriggered &&
                             battleEvent.CardId == ExplosiveReactiveArmourCardRules.CardId)
                        containsReactiveArmour = true;
                    else if (battleEvent?.EventType == BattleEventType.TrapTriggered &&
                             battleEvent.CardId == IronDomeCardRules.CardId)
                        containsIronDome = true;
                    else if (battleEvent?.EventType == BattleEventType.TrapTriggered && battleEvent.CardId == SummonTrapCardRules.CardId)
                        containsSummonTrap = true;
            StartCoroutine(containsRedDragon ? TrackOnlineRedDragonSequence(groups, isGroup: true) :
                containsMeteor ? TrackOnlineMeteorSequence(groups, isGroup: true) :
                containsFireSpear ? TrackOnlineFireSpearSequence(groups, isGroup: true) :
                containsPlasmaShield ? TrackOnlinePlasmaShieldSequence(groups, isGroup: true) :
                containsManaShield ? TrackOnlineManaShieldSequence(groups, isGroup: true) :
                containsReactiveArmour ? TrackOnlineReactiveArmourSequence(groups, isGroup: true) :
                containsIronDome ? TrackOnlineIronDomeSequence(groups, isGroup: true) :
                containsSummonTrap ? TrackOnlineSummonTrapSequence(groups, isGroup: true) : groups);
            return EstimateOnlineBattleEventAnimationGroupsDuration(animationGroups);
        }

        private float PlayOnlineBattleEventAnimationGroup(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var countdown = FindFirstBattleEvent(battleEvents, BattleEventType.TimedBombCountdown);
            if (countdown != null) return PlayOnlineImpactAnimation(countdown.SourceCardId, countdown, battleEvents);
            var areaSpellEffectEvent = FindFirstBattleEvent(
                battleEvents,
                BattleEventType.AreaSpellEffectTriggered);
            if (areaSpellEffectEvent != null)
            {
                return PlayOnlineImpactAnimation(
                    areaSpellEffectEvent.SourceCardId,
                    areaSpellEffectEvent,
                    battleEvents);
            }

            var attackEvent = FindFirstBattleEvent(battleEvents, BattleEventType.AttackStarted);
            if (attackEvent != null)
            {
                return PlayOnlineAttackAnimation(attackEvent, battleEvents);
            }

            var spellEvent = FindFirstBattleEvent(battleEvents, BattleEventType.SpellCast);
            if (spellEvent != null)
            {
                return PlayOnlineImpactAnimation(spellEvent.CardId, spellEvent, battleEvents);
            }

            var trapEvent = FindFirstBattleEvent(battleEvents, BattleEventType.TrapTriggered);
            if (trapEvent != null || HasOnlineImpactEvents(battleEvents))
                return PlayOnlineImpactAnimation(trapEvent?.CardId, trapEvent, battleEvents);

            return _animateOnlineMoveEvents
                ? PlayOnlineMoveAnimations(battleEvents)
                : 0f;
        }

        private IEnumerator PlayOnlineBattleEventAnimationGroups(IReadOnlyList<IReadOnlyList<BattleEventDto>> animationGroups)
        {
            if (animationGroups == null)
            {
                yield break;
            }

            var generation = _combatPresentationGeneration;
            foreach (var animationGroup in animationGroups)
            {
                if (generation != _combatPresentationGeneration) yield break;
                var duration = PlayOnlineBattleEventAnimationGroup(animationGroup);
                if (duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(duration);
                }
                while (generation == _combatPresentationGeneration && (_pendingOnlineRedDragonImpacts > 0 || _pendingOnlineMeteorImpacts > 0 || _pendingOnlineFireSpearImpacts > 0 || _pendingOnlinePlasmaShieldImpacts > 0 || _pendingOnlineManaShieldImpacts > 0 || _pendingOnlineReactiveArmourImpacts > 0 || _pendingOnlineIronDomeImpacts > 0 || _pendingOnlineSummonTrapImpacts > 0))
                    yield return null;
            }
        }

        private float EstimateOnlineBattleEventAnimationGroupsDuration(IReadOnlyList<IReadOnlyList<BattleEventDto>> animationGroups)
        {
            if (animationGroups == null)
            {
                return 0f;
            }

            var duration = 0f;
            foreach (var animationGroup in animationGroups)
            {
                duration += EstimateOnlineBattleEventAnimationGroupDuration(animationGroup);
            }

            return duration;
        }

        private float EstimateOnlineBattleEventAnimationGroupDuration(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return 0f;
            }

            var countdown = FindFirstBattleEvent(battleEvents, BattleEventType.TimedBombCountdown);
            if (countdown != null)
                return EstimateImpactAnimationDuration(BuildOnlineImpactAnimationRequest(countdown.SourceCardId, countdown, battleEvents));

            var areaSpellEffectEvent = FindFirstBattleEvent(
                battleEvents,
                BattleEventType.AreaSpellEffectTriggered);
            if (areaSpellEffectEvent != null)
            {
                var request = BuildOnlineImpactAnimationRequest(
                    areaSpellEffectEvent.SourceCardId,
                    areaSpellEffectEvent,
                    battleEvents);
                return request == null || !HasPresentationImpact(request)
                    ? 0f
                    : EstimateImpactAnimationDuration(request);
            }

            var attackEvent = FindFirstBattleEvent(battleEvents, BattleEventType.AttackStarted);
            if (attackEvent != null)
            {
                var request = BuildOnlineAttackAnimationRequest(attackEvent, battleEvents);
                return request == null ? 0f : EstimateAttackAnimationDuration(request);
            }

            var spellEvent = FindFirstBattleEvent(battleEvents, BattleEventType.SpellCast);
            if (spellEvent != null)
            {
                var request = BuildOnlineImpactAnimationRequest(spellEvent.CardId, spellEvent, battleEvents);
                return request == null || !HasPresentationImpact(request)
                    ? 0f
                    : EstimateImpactAnimationDuration(request);
            }

            var trapEvent = FindFirstBattleEvent(battleEvents, BattleEventType.TrapTriggered);
            if (trapEvent != null || HasOnlineImpactEvents(battleEvents))
            {
                var request = BuildOnlineImpactAnimationRequest(trapEvent?.CardId, trapEvent, battleEvents);
                return request == null || !HasPresentationImpact(request)
                    ? 0f
                    : EstimateImpactAnimationDuration(request);
            }

            return _animateOnlineMoveEvents && FindFirstBattleEvent(battleEvents, BattleEventType.OccupantMoved) != null
                ? _attackRunDuration
                : 0f;
        }

        private static List<IReadOnlyList<BattleEventDto>> BuildOnlineAnimationGroups(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var groups = new List<IReadOnlyList<BattleEventDto>>();
            List<BattleEventDto> currentGroup = null;
            BattleEventDto lastBoardCardPlay = null;

            void FlushCurrentGroup()
            {
                if (currentGroup == null || currentGroup.Count == 0)
                {
                    currentGroup = null;
                    return;
                }

                groups.Add(currentGroup);
                currentGroup = null;
            }

            if (battleEvents == null)
            {
                return groups;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null)
                {
                    continue;
                }

                // Older servers report Dragon damage without an activation cue. Use only
                // those accepted packets, never board state, and avoid duplicating new cues.
                if ((battleEvent.EventType == BattleEventType.DamageApplied || battleEvent.EventType == BattleEventType.InvinciblePrevented) &&
                    battleEvent.ValueCause == BattleValueChangeCause.RedDragon)
                {
                    var cue = FindFirstBattleEvent(currentGroup, BattleEventType.AreaSpellEffectTriggered);
                    var firstDragonPacket = currentGroup?.Find(candidate =>
                        (candidate.EventType == BattleEventType.DamageApplied || candidate.EventType == BattleEventType.InvinciblePrevented) &&
                        candidate.ValueCause == BattleValueChangeCause.RedDragon);
                    if (cue?.EffectId != EndTurnService.RedDragonFlamesEffectId ||
                        (firstDragonPacket != null && firstDragonPacket.SourceRuntimeId != battleEvent.SourceRuntimeId))
                    {
                        FlushCurrentGroup();
                        var targets = new List<TileCoordDto>(BoardState.ColumnCount * BoardState.RowCount);
                        for (var row = 0; row < BoardState.RowCount; row++)
                        for (var column = 0; column < BoardState.ColumnCount; column++)
                            targets.Add(new TileCoordDto { Column = column, Row = row });
                        currentGroup = new List<BattleEventDto> { new BattleEventDto
                        {
                            EventType = BattleEventType.AreaSpellEffectTriggered,
                            EffectId = EndTurnService.RedDragonFlamesEffectId,
                            CardId = RedDragonPresentationAssets.CardId, SourceCardId = RedDragonPresentationAssets.CardId,
                            SourceOwnerId = battleEvent.SourceOwnerId, SourceRuntimeId = battleEvent.SourceRuntimeId,
                            TargetOwnerId = battleEvent.TargetOwnerId, TargetCoord = targets[0], TargetCoords = targets
                        } };
                    }
                }
                if (battleEvent.EventType == BattleEventType.CardPlayed) lastBoardCardPlay = battleEvent;
                if (StartsOnlineAnimationGroup(battleEvent.EventType))
                {
                    FlushCurrentGroup();
                    currentGroup = new List<BattleEventDto> { battleEvent };
                    if (battleEvent.EventType == BattleEventType.TrapTriggered &&
                        battleEvent.CardId == SummonTrapCardRules.CardId && lastBoardCardPlay != null &&
                        lastBoardCardPlay.SourceOwnerId == battleEvent.TargetOwnerId &&
                        lastBoardCardPlay.RuntimeId == battleEvent.TargetRuntimeId)
                        currentGroup.Add(lastBoardCardPlay);
                    continue;
                }

                if (IsOnlineImpactEvent(battleEvent.EventType))
                {
                    currentGroup ??= new List<BattleEventDto>();
                    currentGroup.Add(battleEvent);
                    continue;
                }

                FlushCurrentGroup();
            }

            FlushCurrentGroup();
            AssignTimedBombDeathsToLethalGroup(groups);
            return groups;
        }

        private static void AssignTimedBombDeathsToLethalGroup(List<IReadOnlyList<BattleEventDto>> groups)
        {
            // The server appends final removals after all effects. Keep a confirmed death
            // with the earlier bomb or area effect that reduced this exact occupant to zero HP.
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = (List<BattleEventDto>)groups[groupIndex];
                for (var eventIndex = group.Count - 1; eventIndex >= 0; eventIndex--)
                {
                    var removed = group[eventIndex];
                    if (removed.EventType != BattleEventType.OccupantRemoved) continue;
                    var runtimeId = string.IsNullOrWhiteSpace(removed.TargetRuntimeId) ? removed.RuntimeId : removed.TargetRuntimeId;
                    if (string.IsNullOrWhiteSpace(runtimeId)) continue;
                    for (var earlierIndex = 0; earlierIndex < groupIndex; earlierIndex++)
                    {
                        var earlier = (List<BattleEventDto>)groups[earlierIndex];
                        if (FindFirstBattleEvent(earlier, BattleEventType.TimedBombCountdown) == null &&
                            FindFirstBattleEvent(earlier, BattleEventType.AreaSpellEffectTriggered) == null) continue;
                        var lethal = earlier.Find(candidate => candidate.EventType == BattleEventType.DamageApplied &&
                            candidate.TargetOwnerId == removed.TargetOwnerId && candidate.HpBefore > 0 && candidate.HpAfter <= 0 &&
                            string.Equals(string.IsNullOrWhiteSpace(candidate.TargetRuntimeId) ? candidate.RuntimeId : candidate.TargetRuntimeId,
                                runtimeId, StringComparison.Ordinal));
                        if (lethal == null) continue;
                        earlier.Add(removed); group.RemoveAt(eventIndex); break;
                    }
                }
            }
        }

        private static bool StartsOnlineAnimationGroup(BattleEventType eventType)
        {
            return eventType == BattleEventType.TimedBombCountdown || eventType == BattleEventType.AttackStarted ||
                   eventType == BattleEventType.SpellCast ||
                   eventType == BattleEventType.AreaSpellEffectTriggered ||
                   eventType == BattleEventType.OccupantMoved ||
                   eventType == BattleEventType.TrapTriggered;
        }

        private static bool IsOnlineImpactEvent(BattleEventType eventType)
        {
            return eventType == BattleEventType.DamageApplied ||
                   eventType == BattleEventType.HealingApplied ||
                   eventType == BattleEventType.InvinciblePrevented ||
                   eventType == BattleEventType.OccupantRemoved;
        }

        public void ConfigureOnlineInput(OnlineBattleConnectionTester onlineConnectionTester, bool sendPlayerActionsToOnlineGateway)
        {
            _onlineConnectionTester = onlineConnectionTester;
            _sendPlayerActionsToOnlineGateway = sendPlayerActionsToOnlineGateway;
        }

        private void ConfigureLocalInput()
        {
            _sendPlayerActionsToOnlineGateway = false;
            _onlineConnectionTester = null;
        }

        public void PresentOnlineProjectedState(OnlineBattleConnectionTester onlineConnectionTester)
        {
            if (onlineConnectionTester == null || onlineConnectionTester.CurrentProjectedBattleState == null)
            {
                return;
            }

            if (_onlineConnectionTester != null && !ReferenceEquals(_onlineConnectionTester, onlineConnectionTester))
            {
                return;
            }

            _onlineConnectionTester = onlineConnectionTester;
            _sendPlayerActionsToOnlineGateway = true;
            ReplaceCombatLogEntries(onlineConnectionTester.CombatLogEntries);
            RefreshPresenter();
        }

        public OnlineBattleConnectionTester OnlineConnectionTester => _onlineConnectionTester;

        public bool TryGetCardDefinition(string cardId, out CardDefinition definition)
        {
            definition = null;

            if (_cardDefinitionProvider == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            try
            {
                definition = _cardDefinitionProvider.GetRequired(cardId);
                return definition != null;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetCardDefinitionAsset(string cardId, out CardDefinitionAsset cardAsset)
        {
            cardAsset = null;

            if (_cardCatalogAsset != null &&
                _cardCatalogAsset.TryGetCardAsset(cardId, out cardAsset))
            {
                return true;
            }

            if (string.Equals(cardId, "master", StringComparison.OrdinalIgnoreCase))
            {
                cardAsset = Resources.Load<CardDefinitionAsset>(MasterCardAssetResourcePath);
                return cardAsset != null;
            }

            return false;
        }

        // Chapter three previews local entry; battle practices belong to later chapters.
        public void ConfigureTutorialBattleEntry(IReadOnlyList<string> localDeckCardIds)
        {
            if (!TutorialRuntimeIsolation.IsActive)
                throw new InvalidOperationException("A tutorial host must be active before borrowing the battle scene.");
            if (_tutorialBattleEntry && !_tutorialChapterFour) return;
            ResetTutorialCombatPractice();
            CancelPresentationSequence();
            if (_tutorialMulliganRoutine != null) StopCoroutine(_tutorialMulliganRoutine);
            _tutorialMulliganRoutine = null; _tutorialResolving = false; _tutorialChapterFour = false;
            _tutorialSession = null;
            _battleScreenPresenter?.ResetTutorialPractice();
            _battleScreenPresenter?.TutorialHand?.ConfigureTutorialExpansion(null);
            if (localDeckCardIds == null || localDeckCardIds.Count != 33 || _cardCatalogAsset == null)
                throw new InvalidOperationException("Tutorial battle entry requires its local 33-card deck and native catalog.");
            _tutorialBattleEntry = true;
            _autoRunAiTurns = false;
            _returnToDraftSceneWhenRunSessionActive = false;
            ConfigureLocalInput();
            _cardDefinitionProvider = _cardCatalogAsset.CreateProvider();
            var processor = new BattleCommandProcessor(new PlayCardService(_cardDefinitionProvider),
                new SpellService(_cardDefinitionProvider), new MoveService(), new AttackService(), new EndTurnService());
            _battleFlowController = new BattleFlowController(new BattleSetupService(new TutorialEntryDeckShuffler()),
                new MulliganService(), new TurnStartService(_cardDefinitionProvider), processor);
            _battleGateway = new LocalBattleGateway(_battleFlowController);
            _battleGateway.StartBattle(new BattleSetupRequest(localDeckCardIds, localDeckCardIds, PlayerId.Player));
            _battleGateway.PassMulligan(PlayerId.Player);
            _battleScreenPresenter?.Bind(this);
            _battleScreenPresenter?.SetBattleUiVisible(true);
            ClearCombatLog();
            InitializeTutorialCombatLog();
            RefreshPresenter();
        }

        private sealed class TutorialEntryDeckShuffler : IDeckShuffler
        {
            public void Shuffle(DeckState deckState) { } // Preserve the authored order on every entry.
        }

        public void StartBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds, IReadOnlyList<string> aiDeckCardIds = null)
        {
            if (playerDeckCardIds == null || playerDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("Drafted player deck card ids must be provided before starting the battle.");
            }

            _runtimePlayerDeckCardIdsOverride = new List<string>(playerDeckCardIds);
            _runtimeAIDeckCardIdsOverride = aiDeckCardIds != null && aiDeckCardIds.Count > 0
                ? new List<string>(aiDeckCardIds)
                : Array.Empty<string>();

            StartBattle();
        }

        public void StartOnlineServerAiBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds)
        {
            var isReconnectLaunch = DraftRunSessionState.TryConsumeLastPveReconnect(
                out var reconnectMatchId, out var reconnectRemainingSeconds);
            if (!isReconnectLaunch && (playerDeckCardIds == null || playerDeckCardIds.Count == 0))
            {
                throw new InvalidOperationException("Drafted player deck card ids must be provided before starting the online battle.");
            }

            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();

            _runtimePlayerDeckCardIdsOverride = playerDeckCardIds != null
                ? new List<string>(playerDeckCardIds)
                : new List<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();

            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = null;
            _aiTurnRunner = null;
            _battleFlowController = null;
            _battleGateway = null;

            _sendPlayerActionsToOnlineGateway = true;
            var tester = ResolveOrCreateOnlineConnectionTester();
            ConfigureOnlineInput(tester, sendPlayerActionsToOnlineGateway: true);

            var matchId = isReconnectLaunch ? reconnectMatchId : CreateOnlineDraftMatchId();
            _lastInteractionStatus = "Connecting to server AI battle.";
            Debug.Log($"[After333 Battle Launch] {BuildOnlineLaunchTrace("PVE", matchId, _runtimePlayerDeckCardIdsOverride)}");
            AddCombatLogEntry($"Connecting to server AI battle: {matchId} ({FormatShortRunDeckTrace()})");
            tester.ConnectForServerAiBattle(
                OnlineServerUrl,
                matchId,
                _onlinePlayerToken,
                _runtimePlayerDeckCardIdsOverride,
                isReconnectLaunch && DraftRunSessionState.ActiveBattleIsReconnect,
                reconnectRemainingSeconds,
                DraftRunSessionState.ActiveBattleGameMode,
                DraftRunSessionState.ActiveAiChallengeAttemptId);
            RefreshPresenter();
        }

        public void StartOnlineMatchmakingBattleWithDeckCardIds(IReadOnlyList<string> playerDeckCardIds)
        {
            var hasLocalDeck = playerDeckCardIds != null && playerDeckCardIds.Count == 33;
            var isReconnectLaunch = DraftRunSessionState.ConsumeLastBattleStartWasPvpReconnect() || !hasLocalDeck;
            if (!hasLocalDeck && !AccountSessionState.IsAuthenticated)
            {
                throw new InvalidOperationException("A drafted player deck or authenticated reconnect account is required before entering matchmaking.");
            }

            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();

            _runtimePlayerDeckCardIdsOverride = hasLocalDeck
                ? new List<string>(playerDeckCardIds)
                : new List<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();

            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = null;
            _aiTurnRunner = null;
            _battleFlowController = null;
            _battleGateway = null;

            _sendPlayerActionsToOnlineGateway = true;
            var preparedSession = OnlineBattleSessionCoordinator.Instance;
            if (preparedSession != null && preparedSession.TryAttachToBattle(this))
            {
                _onlineConnectionTester = preparedSession.ConnectionTester;
                _lastInteractionStatus = "Connected to the matched PVP battle.";
                Debug.Log(
                    $"[After333 Battle Launch] attached prepared PVP session match={FormatLogValue(_onlineConnectionTester.CurrentMatchId)}");
                HidePvpMatchmakingOverlay();
                RefreshPresenter();
                return;
            }

            var tester = ResolveOrCreateOnlineConnectionTester();
            ConfigureOnlineInput(tester, sendPlayerActionsToOnlineGateway: true);

            var playerToken = CreateOnlineMatchmakingPlayerToken();
            _lastInteractionStatus = "Waiting for PvP matchmaking or reconnecting to an interrupted PvP battle.";
            Debug.Log($"[After333 Battle Launch] {BuildOnlineLaunchTrace("PVP", "pvp-matchmaking", _runtimePlayerDeckCardIdsOverride)} account={FormatLogValue(AccountSessionState.AccountId)} token={playerToken}");
            ShowPvpMatchmakingOverlay();
            if (isReconnectLaunch)
            {
                SetPvpMatchmakingOverlayMessage("전투에 재접속하는 중...");
            }

            ShowOnlineStatusToast(isReconnectLaunch ? "전투에 재접속하는 중입니다." : "상대를 찾는 중입니다.");
            tester.ConnectForPvpMatchmaking(
                OnlineServerUrl,
                playerToken,
                _runtimePlayerDeckCardIdsOverride);
            RefreshPresenter();
        }

        public void BeginDraft()
        {
            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(false);
            _runtimePlayerDeckCardIdsOverride = Array.Empty<string>();
            _runtimeAIDeckCardIdsOverride = Array.Empty<string>();
            _draftSessionService = new DraftSessionService(_cardCatalogAsset, CreateDraftRandom());
            var validation = _draftSessionService.ValidateCatalog();

            _battleFlowController = null;
            _battleGateway = null;
            _lastInteractionStatus = validation.IsValid ? "Draft started." : validation.Message;
            ClearCombatLog();
            RefreshPresenter();

            if (!validation.IsValid)
            {
                _battleScreenPresenter?.SetBattleUiVisible(true);
                ResolveDraftOverlayPresenter()?.ShowValidationMessage(validation.Message);
                Debug.LogWarning(validation.Message);
                return;
            }

            var openingOffer = _draftSessionService.BeginDraft();
            ResolveDraftOverlayPresenter()?.ShowOffer(openingOffer, _draftSessionService.DeckState.CardIds);
        }

        public void SelectDraftCard(string cardId)
        {
            if (_draftSessionService == null)
            {
                _lastInteractionStatus = "Draft is not active.";
                return;
            }

            try
            {
                var result = _draftSessionService.SelectCard(cardId);
                _lastInteractionStatus = $"Draft picked '{result.SelectedCardId}'.";

                if (result.IsComplete)
                {
                    _draftOverlayPresenter?.Hide();
                    _runtimePlayerDeckCardIdsOverride = new List<string>(result.CompletedDeckCardIds);
                    _lastInteractionStatus = _autoStartBattleAfterDraft
                        ? "Draft complete. Starting battle."
                        : "Draft complete. Press Start Battle when you want to use the drafted deck.";

                    _draftSessionService = null;

                    if (_autoStartBattleAfterDraft)
                    {
                        StartBattleWithDeckCardIds(result.CompletedDeckCardIds);
                        return;
                    }

                    _battleScreenPresenter?.SetBattleUiVisible(true);
                    return;
                }

                ResolveDraftOverlayPresenter()?.ShowOffer(result.NextOffer, _draftSessionService.DeckState.CardIds);
            }
            catch (Exception ex)
            {
                _lastInteractionStatus = ex.Message;
                ResolveDraftOverlayPresenter()?.ShowValidationMessage(ex.Message);
                Debug.LogWarning(ex.Message);
            }
        }

        public void ReturnFromDraftOverlay()
        {
            _draftOverlayPresenter?.Hide();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _lastInteractionStatus = "Draft closed.";
            RefreshPresenter();
        }

        private string OnlineServerUrl =>
            Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveBattleWebSocketUrl(_onlineServerUrl);

        private void Awake()
        {
            if (TutorialRuntimeIsolation.IsActive) return;
            if (UnityEngine.Application.isPlaying && !AccountSessionState.IsAuthenticated)
            {
                SceneManager.LoadScene("GameStart_VSlice");
                return;
            }

            AutoAssignOnlineConnectionTester();

            if (_battleScreenPresenter != null)
            {
                _battleScreenPresenter.Bind(this);
            }

            if (_draftOverlayPresenter != null || _startWithDraftBeforeBattle)
            {
                ResolveDraftOverlayPresenter();
            }

            EnsureOpponentPlayedCardReveal();
            ApplyOpponentPlayedCardRevealSettings();
            EnsureYourTurnBanner();
            ApplyYourTurnBannerSettings();

            if (_startOnAwake)
            {
                if (DraftRunSessionState.TryConsumePendingBattleStart(out var draftedDeckCardIds, out var launchMode))
                {
                    Debug.Log(
                        $"[After333 Battle Launch] consumedPending mode={launchMode} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} runStatus={FormatLogValue(AccountSessionState.ActiveRunStatus)} record={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} deckCards={draftedDeckCardIds?.Count ?? 0}");
                    if (launchMode == DraftBattleLaunchMode.OnlineMatchmaking)
                    {
                        StartOnlineMatchmakingBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                    else if (launchMode == DraftBattleLaunchMode.OnlineServerAi ||
                             launchMode == DraftBattleLaunchMode.OnlineAiChallenge)
                    {
                        StartOnlineServerAiBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                    else
                    {
                        StartBattleWithDeckCardIds(draftedDeckCardIds);
                    }
                }
                else if (_startWithDraftBeforeBattle)
                {
                    BeginDraft();
                }
                else
                {
                    StartBattle();
                }
            }
        }

        private void OnValidate()
        {
            _onlineErrorToastFontSize = Mathf.Max(1f, _onlineErrorToastFontSize);
            _onlineErrorToastSize = new Vector2(
                Mathf.Max(1f, _onlineErrorToastSize.x),
                Mathf.Max(1f, _onlineErrorToastSize.y));
            _onlineErrorToastTextPadding = new Vector2(
                Mathf.Max(0f, _onlineErrorToastTextPadding.x),
                Mathf.Max(0f, _onlineErrorToastTextPadding.y));
            _onlineTurnTimerFontSize = Mathf.Max(1f, _onlineTurnTimerFontSize);
            _onlineTurnTimerSize = new Vector2(
                Mathf.Max(1f, _onlineTurnTimerSize.x),
                Mathf.Max(1f, _onlineTurnTimerSize.y));
            _onlineTurnTimerTextPadding = new Vector2(
                Mathf.Max(0f, _onlineTurnTimerTextPadding.x),
                Mathf.Max(0f, _onlineTurnTimerTextPadding.y));
            _onlineTurnTimerRevealThresholdSeconds = Mathf.Max(0, _onlineTurnTimerRevealThresholdSeconds);
            _onlineTurnTimerDangerThresholdSeconds = Mathf.Max(0, _onlineTurnTimerDangerThresholdSeconds);
            _yourTurnBannerSize = new Vector2(
                Mathf.Max(1f, _yourTurnBannerSize.x),
                Mathf.Max(1f, _yourTurnBannerSize.y));
            _yourTurnBannerFadeInSeconds = Mathf.Max(0f, _yourTurnBannerFadeInSeconds);
            _yourTurnBannerVisibleSeconds = Mathf.Max(0f, _yourTurnBannerVisibleSeconds);
            _yourTurnBannerFadeOutSeconds = Mathf.Max(0f, _yourTurnBannerFadeOutSeconds);
            _opponentPlayedCardRevealSize = new Vector2(
                Mathf.Max(1f, _opponentPlayedCardRevealSize.x),
                Mathf.Max(1f, _opponentPlayedCardRevealSize.y));
            _opponentPlayedCardRevealVisualScale = Mathf.Max(0.1f, _opponentPlayedCardRevealVisualScale);
            _opponentPlayedCardNormalDisplaySeconds = Mathf.Max(0f, _opponentPlayedCardNormalDisplaySeconds);
            _opponentPlayedCardQueuedDisplaySeconds = Mathf.Max(0f, _opponentPlayedCardQueuedDisplaySeconds);
            _opponentPlayedCardFadeInSeconds = Mathf.Max(0f, _opponentPlayedCardFadeInSeconds);
            _opponentPlayedCardFadeOutSeconds = Mathf.Max(0f, _opponentPlayedCardFadeOutSeconds);
            _opponentPlayedCardStatTextSize = new Vector2(
                Mathf.Max(1f, _opponentPlayedCardStatTextSize.x),
                Mathf.Max(1f, _opponentPlayedCardStatTextSize.y));
            _opponentPlayedCardStatFontSize = Mathf.Max(1, _opponentPlayedCardStatFontSize);
            if (UnityEngine.Application.isPlaying)
            {
                ApplyOnlineErrorToastSettings();
                ApplyOnlineTurnTimerSettings();
                ApplyYourTurnBannerSettings();
                ApplyOpponentPlayedCardRevealSettings();
            }
#if UNITY_EDITOR
            else
            {
                QueuePersistentBattleUiMaterialization();
            }
#endif
        }

#if UNITY_EDITOR
        private void QueuePersistentBattleUiMaterialization()
        {
            if (_hasQueuedEditorBattleUiMaterialization)
            {
                return;
            }

            _hasQueuedEditorBattleUiMaterialization = true;
            EditorApplication.delayCall += MaterializePersistentBattleUiForEditor;
        }

        [ContextMenu("Materialize Missing Persistent Battle Runtime UI")]
        public void MaterializePersistentBattleUiForEditor()
        {
            _hasQueuedEditorBattleUiMaterialization = false;
            if (this == null || UnityEngine.Application.isPlaying)
            {
                return;
            }

            EnsureOnlineErrorToast();
            EnsureOnlineTurnTimer();
            EnsureYourTurnBanner();
            EnsureOpponentPlayedCardReveal();
            EnsurePvpMatchmakingOverlay();

            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif

        private void EnsureOnlineErrorToast()
        {
            if (_onlineErrorToastCanvasGroup != null && _onlineErrorToastText != null)
            {
                return;
            }

            _onlineErrorToastUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OnlineErrorToastCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Online Error Toast UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20000;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _onlineErrorToastCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _onlineErrorToastCanvasGroup.alpha = 0f;
            _onlineErrorToastCanvasGroup.interactable = false;
            _onlineErrorToastCanvasGroup.blocksRaycasts = false;

            var panelObject = new GameObject("OnlineErrorToastPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _onlineErrorToastPanelRect = panelObject.GetComponent<RectTransform>();
            _onlineErrorToastPanelImage = panelObject.GetComponent<Image>();
            _onlineErrorToastPanelImage.raycastTarget = false;

            var textObject = new GameObject("OnlineErrorToastText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _onlineErrorToastTextRect = textObject.GetComponent<RectTransform>();

            _onlineErrorToastText = textObject.GetComponent<Text>();
            _onlineErrorToastText.alignment = TextAnchor.MiddleCenter;
            _onlineErrorToastText.fontStyle = FontStyle.Bold;
            _onlineErrorToastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _onlineErrorToastText.verticalOverflow = VerticalWrapMode.Truncate;
            _onlineErrorToastText.supportRichText = false;
            _onlineErrorToastText.raycastTarget = false;
            ApplyOnlineErrorToastSettings();

            if (!UnityEngine.Application.isPlaying)
            {
                _onlineErrorToastUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private void EnsureOnlineTurnTimer()
        {
            if (_onlineTurnTimerCanvasGroup != null && _onlineTurnTimerText != null)
            {
                return;
            }

            _onlineTurnTimerUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OnlineTurnTimerCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Online Turn Timer UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 19990;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _onlineTurnTimerCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _onlineTurnTimerCanvasGroup.alpha = 0f;
            _onlineTurnTimerCanvasGroup.interactable = false;
            _onlineTurnTimerCanvasGroup.blocksRaycasts = false;

            var panelObject = new GameObject("OnlineTurnTimerPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _onlineTurnTimerPanelRect = panelObject.GetComponent<RectTransform>();
            _onlineTurnTimerPanelImage = panelObject.GetComponent<Image>();
            _onlineTurnTimerPanelImage.raycastTarget = false;

            var textObject = new GameObject("OnlineTurnTimerText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _onlineTurnTimerTextRect = textObject.GetComponent<RectTransform>();

            _onlineTurnTimerText = textObject.GetComponent<Text>();
            _onlineTurnTimerText.alignment = TextAnchor.MiddleCenter;
            _onlineTurnTimerText.fontStyle = FontStyle.Bold;
            _onlineTurnTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _onlineTurnTimerText.verticalOverflow = VerticalWrapMode.Truncate;
            _onlineTurnTimerText.supportRichText = false;
            _onlineTurnTimerText.raycastTarget = false;
            ApplyOnlineTurnTimerSettings();

            if (!UnityEngine.Application.isPlaying)
            {
                _onlineTurnTimerUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private void EnsureYourTurnBanner()
        {
            if (_yourTurnBannerCanvasGroup != null &&
                _yourTurnBannerRect != null &&
                _yourTurnBannerImage != null)
            {
                return;
            }

            _yourTurnBannerUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "YourTurnBannerCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Your Turn Banner UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = _yourTurnBannerSortingOrder;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _yourTurnBannerCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _yourTurnBannerCanvasGroup.alpha = 0f;
            _yourTurnBannerCanvasGroup.interactable = false;
            _yourTurnBannerCanvasGroup.blocksRaycasts = false;

            var imageObject = new GameObject("YourTurnBannerImage", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            _yourTurnBannerRect = imageObject.GetComponent<RectTransform>();
            _yourTurnBannerImage = imageObject.GetComponent<Image>();
            _yourTurnBannerImage.preserveAspect = true;
            _yourTurnBannerImage.raycastTarget = false;

            ApplyYourTurnBannerSettings();

            if (!UnityEngine.Application.isPlaying)
            {
                _yourTurnBannerUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private void EnsureOpponentPlayedCardReveal()
        {
            if (_opponentPlayedCardRevealCanvasGroup != null &&
                _opponentPlayedCardRevealImage != null &&
                _opponentPlayedCardFallbackText != null &&
                _opponentPlayedCardAttackText != null &&
                _opponentPlayedCardHpText != null)
            {
                return;
            }

            _opponentPlayedCardRevealUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "OpponentPlayedCardRevealCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create Opponent Played Card Reveal UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = ResolveOpponentPlayedCardSortingOrder();

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _opponentPlayedCardRevealCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _opponentPlayedCardRevealCanvasGroup.alpha = 0f;
            _opponentPlayedCardRevealCanvasGroup.interactable = false;
            _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;

            var cardObject = new GameObject("CardImage", typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(canvasObject.transform, false);
            _opponentPlayedCardRevealRect = cardObject.GetComponent<RectTransform>();
            _opponentPlayedCardRevealImage = cardObject.GetComponent<Image>();
            _opponentPlayedCardRevealImage.preserveAspect = true;
            _opponentPlayedCardRevealImage.raycastTarget = false;

            var fallbackObject = new GameObject("FallbackCardIdText", typeof(RectTransform), typeof(Text));
            fallbackObject.transform.SetParent(cardObject.transform, false);
            _opponentPlayedCardFallbackText = fallbackObject.GetComponent<Text>();
            _opponentPlayedCardFallbackText.alignment = TextAnchor.MiddleCenter;
            _opponentPlayedCardFallbackText.fontStyle = FontStyle.Bold;
            _opponentPlayedCardFallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _opponentPlayedCardFallbackText.verticalOverflow = VerticalWrapMode.Truncate;
            _opponentPlayedCardFallbackText.supportRichText = false;
            _opponentPlayedCardFallbackText.raycastTarget = false;

            _opponentPlayedCardAttackText = CreateOpponentPlayedCardStatText(
                cardObject.transform,
                "AttackValueText");
            _opponentPlayedCardHpText = CreateOpponentPlayedCardStatText(
                cardObject.transform,
                "HpValueText");

            ApplyOpponentPlayedCardRevealSettings();
            if (!UnityEngine.Application.isPlaying)
            {
                _opponentPlayedCardRevealUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private Text CreateOpponentPlayedCardStatText(Transform parent, string objectName)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private void EnsurePvpMatchmakingOverlay()
        {
            if (_pvpMatchmakingCanvasGroup != null && _pvpMatchmakingText != null && _pvpMatchmakingCancelButton != null)
            {
                _pvpMatchmakingCancelButton.onClick.RemoveListener(CancelPvpMatchmakingFromUi);
                _pvpMatchmakingCancelButton.onClick.AddListener(CancelPvpMatchmakingFromUi);
                ApplyPvpMatchmakingOverlaySettings();
                return;
            }

            _pvpMatchmakingUsesSceneLayout = false;

            var canvasObject = new GameObject(
                "PvpMatchmakingOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            MoveCreatedObjectToBattleScene(canvasObject);
            RegisterEditorCreatedObject(canvasObject, "Create PVP Matchmaking UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20010;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 1f;

            _pvpMatchmakingCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _pvpMatchmakingCanvasGroup.alpha = 0f;
            _pvpMatchmakingCanvasGroup.interactable = false;
            _pvpMatchmakingCanvasGroup.blocksRaycasts = false;

            var dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimObject.transform.SetParent(canvasObject.transform, false);
            _pvpMatchmakingDimRect = dimObject.GetComponent<RectTransform>();
            _pvpMatchmakingDimImage = dimObject.GetComponent<Image>();

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _pvpMatchmakingPanelRect = panelObject.GetComponent<RectTransform>();
            _pvpMatchmakingPanelImage = panelObject.GetComponent<Image>();
            _pvpMatchmakingPanelOutline = panelObject.GetComponent<Outline>();

            var textObject = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            _pvpMatchmakingTextRect = textObject.GetComponent<RectTransform>();
            _pvpMatchmakingText = textObject.GetComponent<Text>();
            _pvpMatchmakingText.alignment = TextAnchor.MiddleCenter;
            _pvpMatchmakingText.fontStyle = FontStyle.Bold;
            _pvpMatchmakingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pvpMatchmakingText.verticalOverflow = VerticalWrapMode.Truncate;
            _pvpMatchmakingText.supportRichText = false;
            _pvpMatchmakingText.raycastTarget = false;

            var cancelButtonObject = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelButtonObject.transform.SetParent(panelObject.transform, false);
            _pvpMatchmakingCancelButtonRect = cancelButtonObject.GetComponent<RectTransform>();
            _pvpMatchmakingCancelButtonImage = cancelButtonObject.GetComponent<Image>();
            _pvpMatchmakingCancelButton = cancelButtonObject.GetComponent<Button>();
            _pvpMatchmakingCancelButton.onClick.AddListener(CancelPvpMatchmakingFromUi);

            var cancelTextObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            cancelTextObject.transform.SetParent(cancelButtonObject.transform, false);
            var cancelTextRect = cancelTextObject.GetComponent<RectTransform>();
            cancelTextRect.anchorMin = Vector2.zero;
            cancelTextRect.anchorMax = Vector2.one;
            cancelTextRect.offsetMin = Vector2.zero;
            cancelTextRect.offsetMax = Vector2.zero;

            _pvpMatchmakingCancelButtonText = cancelTextObject.GetComponent<Text>();
            _pvpMatchmakingCancelButtonText.alignment = TextAnchor.MiddleCenter;
            _pvpMatchmakingCancelButtonText.fontStyle = FontStyle.Bold;
            _pvpMatchmakingCancelButtonText.supportRichText = false;
            _pvpMatchmakingCancelButtonText.raycastTarget = false;

            ApplyPvpMatchmakingOverlaySettings();
            if (!UnityEngine.Application.isPlaying)
            {
                _pvpMatchmakingUsesSceneLayout = true;
            }

            canvasObject.SetActive(false);
        }

        private static void RegisterEditorCreatedObject(GameObject createdObject, string undoLabel)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && createdObject != null)
            {
                Undo.RegisterCreatedObjectUndo(createdObject, undoLabel);
            }
#endif
        }

        private void MoveCreatedObjectToBattleScene(GameObject createdObject)
        {
            if (createdObject != null && gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(createdObject, gameObject.scene);
            }
        }

        private void ApplyOnlineErrorToastSettings()
        {
            if (_onlineErrorToastPanelRect != null && !_onlineErrorToastUsesSceneLayout)
            {
                var anchor = new Vector2(
                    Mathf.Clamp01(_onlineErrorToastAnchor.x),
                    Mathf.Clamp01(_onlineErrorToastAnchor.y));
                _onlineErrorToastPanelRect.anchorMin = anchor;
                _onlineErrorToastPanelRect.anchorMax = anchor;
                _onlineErrorToastPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _onlineErrorToastPanelRect.anchoredPosition = _onlineErrorToastOffset;
                _onlineErrorToastPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _onlineErrorToastSize.x),
                    Mathf.Max(1f, _onlineErrorToastSize.y));
            }

            if (_onlineErrorToastPanelImage != null)
            {
                _onlineErrorToastPanelImage.color = NoticeTheme.PanelColor;
                _onlineErrorToastPanelImage.raycastTarget = false;
            }

            if (_onlineErrorToastTextRect != null && !_onlineErrorToastUsesSceneLayout)
            {
                var horizontalPadding = Mathf.Max(0f, _onlineErrorToastTextPadding.x);
                var verticalPadding = Mathf.Max(0f, _onlineErrorToastTextPadding.y);
                _onlineErrorToastTextRect.anchorMin = Vector2.zero;
                _onlineErrorToastTextRect.anchorMax = Vector2.one;
                _onlineErrorToastTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _onlineErrorToastTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_onlineErrorToastText != null)
            {
                var resolvedFont = ResolveOnlineErrorToastFont();
                if (resolvedFont != null)
                {
                    _onlineErrorToastText.font = resolvedFont;
                }

                _onlineErrorToastText.color = _onlineErrorToastTextColor;
                _onlineErrorToastText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_onlineErrorToastFontSize));
            }
            NoticeTheme.Toast(_onlineErrorToastPanelRect, _onlineErrorToastText);
        }

        private Font ResolveOnlineErrorToastFont()
        {
            if (_onlineErrorToastFont != null)
            {
                return _onlineErrorToastFont;
            }

            if (s_runtimeKoreanToastFont != null)
            {
                return s_runtimeKoreanToastFont;
            }

            try
            {
                s_runtimeKoreanToastFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not create runtime Korean font for online error toast: {ex.Message}");
                s_runtimeKoreanToastFont = null;
            }

            return s_runtimeKoreanToastFont != null
                ? s_runtimeKoreanToastFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void ApplyOnlineTurnTimerSettings()
        {
            if (_onlineTurnTimerPanelRect != null && !_onlineTurnTimerUsesSceneLayout)
            {
                var anchor = new Vector2(
                    Mathf.Clamp01(_onlineTurnTimerAnchor.x),
                    Mathf.Clamp01(_onlineTurnTimerAnchor.y));
                _onlineTurnTimerPanelRect.anchorMin = anchor;
                _onlineTurnTimerPanelRect.anchorMax = anchor;
                _onlineTurnTimerPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _onlineTurnTimerPanelRect.anchoredPosition = _onlineTurnTimerOffset;
                _onlineTurnTimerPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _onlineTurnTimerSize.x),
                    Mathf.Max(1f, _onlineTurnTimerSize.y));
            }

            if (_onlineTurnTimerPanelImage != null)
            {
                _onlineTurnTimerPanelImage.raycastTarget = false;
            }

            if (_onlineTurnTimerTextRect != null && !_onlineTurnTimerUsesSceneLayout)
            {
                var horizontalPadding = Mathf.Max(0f, _onlineTurnTimerTextPadding.x);
                var verticalPadding = Mathf.Max(0f, _onlineTurnTimerTextPadding.y);
                _onlineTurnTimerTextRect.anchorMin = Vector2.zero;
                _onlineTurnTimerTextRect.anchorMax = Vector2.one;
                _onlineTurnTimerTextRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
                _onlineTurnTimerTextRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            }

            if (_onlineTurnTimerText != null)
            {
                var resolvedFont = ResolveOnlineErrorToastFont();
                if (resolvedFont != null)
                {
                    _onlineTurnTimerText.font = resolvedFont;
                }

                _onlineTurnTimerText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_onlineTurnTimerFontSize));
            }
            NoticeTheme.Inline(_onlineTurnTimerPanelRect, _onlineTurnTimerText);
        }

        private void ApplyYourTurnBannerSettings()
        {
            if (_yourTurnBannerCanvasGroup != null)
            {
                var canvas = _yourTurnBannerCanvasGroup.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = _yourTurnBannerSortingOrder;
                }

                _yourTurnBannerCanvasGroup.interactable = false;
                _yourTurnBannerCanvasGroup.blocksRaycasts = false;
            }

            if (_yourTurnBannerSprite == null)
            {
                _yourTurnBannerSprite = Resources.Load<Sprite>(YourTurnBannerResourcePath);
            }

            if (_yourTurnBannerImage != null)
            {
                _yourTurnBannerImage.overrideSprite = null;
                _yourTurnBannerImage.sprite = _yourTurnBannerSprite;
                _yourTurnBannerImage.type = UnityEngine.UI.Image.Type.Simple;
                var outline = _yourTurnBannerImage.GetComponent<UnityEngine.UI.Outline>();
                if (outline != null) outline.enabled = false;
                _yourTurnBannerImage.color = Color.white;
                _yourTurnBannerImage.preserveAspect = true;
                _yourTurnBannerImage.raycastTarget = false;
            }

            // A banner saved while the common notice theme was active may retain its label.
            var noticeLabel = _yourTurnBannerRect != null ? _yourTurnBannerRect.Find("NoticeLabel") : null;
            if (noticeLabel != null)
            {
                noticeLabel.gameObject.SetActive(false);
                _yourTurnBannerRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _yourTurnBannerSize.x),
                    Mathf.Max(1f, _yourTurnBannerSize.y));
            }

            if (_yourTurnBannerRect == null || _yourTurnBannerUsesSceneLayout)
            {
                return;
            }

            _yourTurnBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
            _yourTurnBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _yourTurnBannerRect.pivot = new Vector2(0.5f, 0.5f);
            _yourTurnBannerRect.anchoredPosition = _yourTurnBannerOffset;
            _yourTurnBannerRect.sizeDelta = new Vector2(
                Mathf.Max(1f, _yourTurnBannerSize.x),
                Mathf.Max(1f, _yourTurnBannerSize.y));
        }

        private int ResolveOpponentPlayedCardSortingOrder()
        {
            return _opponentPlayedCardSortingOrder == LegacyOpponentPlayedCardSortingOrder
                ? DefaultOpponentPlayedCardSortingOrder
                : _opponentPlayedCardSortingOrder;
        }

        private void ApplyOpponentPlayedCardRevealSettings()
        {
            if (_opponentPlayedCardRevealCanvasGroup != null)
            {
                var canvas = _opponentPlayedCardRevealCanvasGroup.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = ResolveOpponentPlayedCardSortingOrder();
                }

                _opponentPlayedCardRevealCanvasGroup.interactable = false;
                _opponentPlayedCardRevealCanvasGroup.blocksRaycasts = false;
            }

            if (_opponentPlayedCardRevealImage != null)
            {
                _opponentPlayedCardRevealImage.preserveAspect = true;
                _opponentPlayedCardRevealImage.raycastTarget = false;
            }

            if (_opponentPlayedCardRevealRect != null)
            {
                _opponentPlayedCardRevealRect.localScale = Vector3.one *
                    Mathf.Max(0.1f, _opponentPlayedCardRevealVisualScale);
            }

            if (_opponentPlayedCardRevealUsesSceneLayout)
            {
                return;
            }

            if (_opponentPlayedCardRevealRect != null)
            {
                _opponentPlayedCardRevealRect.anchorMin = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.anchorMax = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.pivot = new Vector2(0.5f, 0.5f);
                _opponentPlayedCardRevealRect.anchoredPosition = Vector2.zero;
                _opponentPlayedCardRevealRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _opponentPlayedCardRevealSize.x),
                    Mathf.Max(1f, _opponentPlayedCardRevealSize.y));
            }

            var font = ResolveOnlineErrorToastFont();
            if (_opponentPlayedCardFallbackText != null)
            {
                var fallbackRect = _opponentPlayedCardFallbackText.rectTransform;
                fallbackRect.anchorMin = Vector2.zero;
                fallbackRect.anchorMax = Vector2.one;
                fallbackRect.offsetMin = new Vector2(18f, 18f);
                fallbackRect.offsetMax = new Vector2(-18f, -18f);
                _opponentPlayedCardFallbackText.font = font;
                _opponentPlayedCardFallbackText.fontSize = 30;
                _opponentPlayedCardFallbackText.color = Color.white;
            }

            ApplyOpponentPlayedCardStatSettings(
                _opponentPlayedCardAttackText,
                _opponentPlayedCardAttackStatNormalizedPosition,
                font);
            ApplyOpponentPlayedCardStatSettings(
                _opponentPlayedCardHpText,
                _opponentPlayedCardHpStatNormalizedPosition,
                font);
        }

        private void ApplyOpponentPlayedCardStatSettings(Text text, Vector2 normalizedPosition, Font font)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            var anchor = new Vector2(
                Mathf.Clamp01(normalizedPosition.x),
                Mathf.Clamp01(normalizedPosition.y));
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = _opponentPlayedCardStatTextSize;

            text.font = font;
            text.fontSize = Mathf.Max(1, _opponentPlayedCardStatFontSize);
            text.color = _opponentPlayedCardStatTextColor;
            if (text.TryGetComponent<Outline>(out var outline))
            {
                outline.effectColor = _opponentPlayedCardStatOutlineColor;
                outline.effectDistance = _opponentPlayedCardStatOutlineDistance;
                outline.useGraphicAlpha = true;
            }
        }

        private void ApplyPvpMatchmakingOverlaySettings()
        {
            if (_pvpMatchmakingDimRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingDimRect.anchorMin = Vector2.zero;
                _pvpMatchmakingDimRect.anchorMax = Vector2.one;
                _pvpMatchmakingDimRect.offsetMin = Vector2.zero;
                _pvpMatchmakingDimRect.offsetMax = Vector2.zero;
            }

            if (_pvpMatchmakingDimImage != null)
            {
                _pvpMatchmakingDimImage.color = _pvpMatchmakingDimColor;
                _pvpMatchmakingDimImage.raycastTarget = true;
            }

            if (_pvpMatchmakingPanelRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.pivot = new Vector2(0.5f, 0.5f);
                _pvpMatchmakingPanelRect.anchoredPosition = _pvpMatchmakingPanelOffset;
                _pvpMatchmakingPanelRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _pvpMatchmakingPanelSize.x),
                    Mathf.Max(1f, _pvpMatchmakingPanelSize.y));
            }

            if (_pvpMatchmakingPanelImage != null)
            {
                _pvpMatchmakingPanelImage.color = _pvpMatchmakingPanelColor;
                _pvpMatchmakingPanelImage.raycastTarget = true;
            }

            if (_pvpMatchmakingPanelOutline != null)
            {
                _pvpMatchmakingPanelOutline.effectColor = _pvpMatchmakingPanelOutlineColor;
                _pvpMatchmakingPanelOutline.effectDistance = new Vector2(2f, -2f);
            }

            if (_pvpMatchmakingTextRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingTextRect.anchorMin = Vector2.zero;
                _pvpMatchmakingTextRect.anchorMax = Vector2.one;
                _pvpMatchmakingTextRect.offsetMin = new Vector2(
                    Mathf.Max(0f, _pvpMatchmakingTextPadding.x),
                    Mathf.Max(0f, _pvpMatchmakingTextPadding.y));
                _pvpMatchmakingTextRect.offsetMax = new Vector2(
                    -Mathf.Max(0f, _pvpMatchmakingTextPadding.x),
                    0f);
            }

            var resolvedFont = ResolveOnlineErrorToastFont();
            if (_pvpMatchmakingText != null)
            {
                if (resolvedFont != null)
                {
                    _pvpMatchmakingText.font = resolvedFont;
                }

                _pvpMatchmakingText.color = _pvpMatchmakingTextColor;
                _pvpMatchmakingText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_pvpMatchmakingTextFontSize));
            }

            if (_pvpMatchmakingCancelButtonRect != null && !_pvpMatchmakingUsesSceneLayout)
            {
                _pvpMatchmakingCancelButtonRect.anchorMin = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.anchorMax = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.pivot = new Vector2(0.5f, 0f);
                _pvpMatchmakingCancelButtonRect.anchoredPosition = _pvpMatchmakingCancelButtonOffset;
                _pvpMatchmakingCancelButtonRect.sizeDelta = new Vector2(
                    Mathf.Max(1f, _pvpMatchmakingCancelButtonSize.x),
                    Mathf.Max(1f, _pvpMatchmakingCancelButtonSize.y));
            }

            if (_pvpMatchmakingCancelButtonImage != null)
            {
                _pvpMatchmakingCancelButtonImage.color = _pvpMatchmakingCancelButtonColor;
                _pvpMatchmakingCancelButtonImage.raycastTarget = true;
            }

            if (_pvpMatchmakingCancelButtonText != null)
            {
                if (resolvedFont != null)
                {
                    _pvpMatchmakingCancelButtonText.font = resolvedFont;
                }

                _pvpMatchmakingCancelButtonText.text = _pvpMatchmakingCancelButtonLabel;
                _pvpMatchmakingCancelButtonText.color = _pvpMatchmakingTextColor;
                _pvpMatchmakingCancelButtonText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_pvpMatchmakingCancelFontSize));
            }
            NoticeTheme.Dialog(_pvpMatchmakingPanelRect);
        }

        private IEnumerator OnlineErrorToastSequence(bool resumeReconnectCountdownAfter)
        {
            if (_onlineErrorToastCanvasGroup == null)
            {
                yield break;
            }

            _onlineErrorToastCanvasGroup.gameObject.SetActive(true);
            _onlineErrorToastCanvasGroup.alpha = 0f;

            yield return FadeOnlineErrorToast(0f, 1f, OnlineErrorToastFadeInSeconds);
            yield return new WaitForSecondsRealtime(OnlineErrorToastVisibleSeconds);
            yield return FadeOnlineErrorToast(1f, 0f, OnlineErrorToastFadeOutSeconds);

            if (resumeReconnectCountdownAfter && TryResumeOpponentReconnectCountdown())
            {
                _onlineErrorToastCoroutine = null;
                yield break;
            }

            if (_onlineErrorToastCanvasGroup != null)
            {
                _onlineErrorToastCanvasGroup.alpha = 0f;
                _onlineErrorToastCanvasGroup.gameObject.SetActive(false);
            }

            _onlineErrorToastCoroutine = null;
        }

        private IEnumerator FadeOnlineErrorToast(float fromAlpha, float toAlpha, float durationSeconds)
        {
            if (_onlineErrorToastCanvasGroup == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _onlineErrorToastCanvasGroup.alpha = toAlpha;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / durationSeconds);
                _onlineErrorToastCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                yield return null;
            }

            _onlineErrorToastCanvasGroup.alpha = toAlpha;
        }

        public void StartBattle()
        {
            HideYourTurnBanner();

            if (_startWithDraftBeforeBattle && (_runtimePlayerDeckCardIdsOverride == null || _runtimePlayerDeckCardIdsOverride.Count == 0))
            {
                BeginDraft();
                return;
            }

            ConfigureLocalInput();
            CancelPresentationSequence();
            _battleScreenPresenter?.SetBattleUiVisible(true);
            _draftOverlayPresenter?.Hide();
            _hasQueuedDraftSceneReturn = false;
            ClearCombatLog();
            var resolvedConfig = ResolveStartConfig();
            _cardDefinitionProvider = resolvedConfig.CardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            _aiDecisionService = new AiDecisionService(_cardDefinitionProvider);
            _aiTurnRunner = new AiTurnRunner(_aiDecisionService);

            _battleFlowController = CreateBattleFlowController(_cardDefinitionProvider);
            _battleGateway = new LocalBattleGateway(_battleFlowController);
            var firstPlayerId = _firstPlayerSelector.SelectFirstPlayer();
            _battleGateway.StartBattle(new BattleSetupRequest(
                playerDeckCardIds: resolvedConfig.PlayerDeckCardIds,
                aiDeckCardIds: resolvedConfig.AIDeckCardIds,
                firstPlayerId: firstPlayerId));

            AddCombatLogEntry(CombatLogFormatter.FormatBattleStarted(firstPlayerId));

            if (_autoPassPlayerMulligan && CurrentBattleState.Phase == PhaseType.Mulligan)
            {
                _battleGateway.PassMulligan(PlayerId.Player);
            }

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            }

            _lastInteractionStatus = "Battle started.";
            RefreshPresenter();
            MaybeRunAiTurn();
        }

        [ContextMenu("Fill Debug Decks")]
        public void FillDebugDecks()
        {
            _playerDeckAsset = null;
            _aiDeckAsset = null;
            _playerDeckCardIds = BattleDebugDeckFactory.CreateDeck("P", _generatedDeckSize);
            _aiDeckCardIds = BattleDebugDeckFactory.CreateDeck("A", _generatedDeckSize);
        }

        public void PassPlayerMulligan()
        {
            SubmitPlayerMulligan(Array.Empty<string>());
        }

        public bool SubmitPlayerMulligan(IReadOnlyList<string> selectedCardIds)
        {
            if (_tutorialBattleEntry) return SubmitTutorialMulligan(selectedCardIds);
            if (!TryBeginImmediateInteraction())
            {
                return false;
            }

            EnsureBattleStarted();
            if (CurrentBattleState.Phase != PhaseType.Mulligan || CurrentBattleState.Player.HasUsedMulligan)
            {
                _lastInteractionStatus = "Mulligan has already been confirmed.";
                return false;
            }

            var replacements = selectedCardIds == null
                ? new List<string>()
                : new List<string>(selectedCardIds);

            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendMulligan(replacements, out var onlineMessage))
                {
                    _lastInteractionStatus = onlineMessage;
                    return true;
                }

                _lastInteractionStatus = onlineMessage;
                ShowCommandErrorToast(onlineMessage);
                return false;
            }

            if (replacements.Count == 0)
            {
                _battleGateway.PassMulligan(PlayerId.Player);
            }
            else
            {
                _battleGateway.ApplyMulligan(PlayerId.Player, replacements, new SystemDeckShuffler());
            }

            if (_autoResolveTurnStartAfterAutoPass && CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
            MaybeRunAiTurn();
            return true;
        }

        public void ResolveTurnStart()
        {
            if (!TryBeginImmediateInteraction())
            {
                return;
            }

            EnsureBattleStarted();
            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            _battleGateway.ResolveTurnStart();
            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(CurrentBattleState.ActivePlayerId, CurrentBattleState.TurnNumber));
            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
            if (ShouldAnimateAttackSequences() && HasPresentationImpact(impactRequest))
            {
                StartPresentationSequence(PresentationOnlySequence(impactRequest));
                return;
            }

            RefreshPresenter();
            MaybeRunAiTurn();
        }

        public void EndTurn()
        {
            if (!CanPlayerEndTurn)
            {
                return;
            }

            EnsureBattleStarted();
            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendEndTurn(out var onlineMessage))
                {
                    AddCombatLogEntry(onlineMessage);
                }

                _lastInteractionStatus = onlineMessage;
                return;
            }

            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(CurrentBattleState.ActivePlayerId));
            _endTurnSubmittedBattle = CurrentBattleState;
            _endTurnSubmittedNumber = CurrentBattleState.TurnNumber;
            try { _battleGateway.EndTurn(); }
            catch
            {
                _endTurnSubmittedBattle = null;
                _endTurnSubmittedNumber = -1;
                throw;
            }
            _battleScreenPresenter?.QueueTemporaryReplicateExpiry();
            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
            if (ShouldAnimateAttackSequences() && HasPresentationImpact(impactRequest))
            {
                StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                return;
            }

            RefreshPresenter();
            if (_tutorialChapterFour) StartPresentationSequence(TutorialOpponentTurnSequence());
            else MaybeRunAiTurn();
        }

        public bool TrySurrenderPlayer(out string message)
        {
            if (_tutorialBattleEntry) { message = "연습 전투에서는 튜토리얼 스킵을 이용하세요."; return false; }
            var battleState = CurrentBattleState;
            if (battleState == null)
            {
                message = "진행 중인 전투가 없습니다.";
                ShowCommandErrorToast(message);
                return false;
            }

            if (battleState.IsEnded)
            {
                message = "이미 종료된 전투입니다.";
                ShowCommandErrorToast(message);
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                if (_onlineConnectionTester.TrySendSurrender(out message))
                {
                    _lastInteractionStatus = message;
                    return true;
                }

                ShowCommandErrorToast(message);
                return false;
            }

            battleState.EndBattle(PlayerId.AI);
            message = "항복했습니다.";
            _lastInteractionStatus = message;
            AddCombatLogEntry(message);
            RefreshPresenter();
            return true;
        }

        public bool TryActivatePlayerEffect(TileCoord coord, string runtimeId, out string message)
        {
            if (_tutorialBattleEntry) { message = "이번 실습에서는 액티브를 사용하지 않습니다."; return false; }
            EnsureBattleStarted();
            if (IsBusy) { message = BusyMessage; return false; }
            if (ActiveEffectService.CanOfferActivation(CurrentBattleState, PlayerId.Player, coord))
            {
                var costFailure = ActiveEffectService.GetCostFailureMessage(CurrentBattleState, PlayerId.Player, coord);
                if (!string.IsNullOrEmpty(costFailure))
                    return FailPlayerCardUse(costFailure, out message);
            }
            var command = new ActivateEffectCommand(coord, runtimeId);
            if (ShouldRoutePlayerActionsOnline())
                return TrySendOnlinePlayerCommand(command, "액티브 사용 요청", out message);
            try
            {
                var activeLog = CombatLogFormatter.FormatCommand(PlayerId.Player, command,
                    CurrentBattleState, ResolveCombatLogCardDisplayName);
                _battleGateway.ExecuteCommand(PlayerId.Player, command);
                message = activeLog;
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                return true;
            }
            catch (Exception ex) { return FailPlayerCardUse(ex.Message, out message); }
        }

        public bool TryMovePlayerOccupant(TileCoord fromCoord, TileCoord toCoord, out string message)
        {
            EnsureBattleStarted();
            if (!TutorialAllowsMove(fromCoord, toCoord)) { message = "초록색으로 표시한 실습 대상을 이용하세요."; return false; }

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                return TrySendOnlinePlayerCommand(
                    new MoveOccupantCommand(fromCoord, toCoord),
                    $"Sent online move from {fromCoord} to {toCoord}.",
                    out message);
            }

            try
            {
                var isSwap = CurrentBattleState.PlayerBoard.GetOccupant(toCoord) != null;
                _battleGateway.ExecuteCommand(PlayerId.Player, new MoveOccupantCommand(fromCoord, toCoord));
                _battleScreenPresenter?.PlayUnitMoveSound();
                message = isSwap
                    ? CombatLogFormatter.FormatSwap(fromCoord, toCoord)
                    : $"Moved unit from {fromCoord} to {toCoord}.";
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                TutorialMoveAccepted();
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                TutorialMoveRejected(fromCoord, toCoord);
                message = ex.Message;
                _lastInteractionStatus = message;
                Debug.LogWarning($"Move interaction failed from {fromCoord} to {toCoord}: {ex.Message}");
                return false;
            }
        }

        public bool TryAttackWithPlayerOccupant(TileCoord attackerCoord, TileCoord targetCoord, out string message)
        {
            EnsureBattleStarted();
            if (!TutorialAllowsAttack(attackerCoord, targetCoord)) { message = "초록색으로 표시한 실습 대상을 이용하세요."; return false; }

            if (IsBusy)
            {
                message = BusyMessage;
                _lastInteractionStatus = message;
                return false;
            }

            if (ShouldRoutePlayerActionsOnline())
            {
                return TrySendOnlinePlayerCommand(
                    new AttackCommand(attackerCoord, targetCoord),
                    $"Sent online attack from {attackerCoord} to {targetCoord}.",
                    out message);
            }

            try
            {
                var attackCommand = new AttackCommand(attackerCoord, targetCoord);
                if (!ShouldAnimateAttackSequences())
                {
                    _battleGateway.ExecuteCommand(PlayerId.Player, attackCommand);
                    message = CurrentBattleState.LastAttackResolution?.WasCancelledByTrap == true
                        ? "트랩이 발동해 공격이 취소되었습니다." : $"Attacked from {attackerCoord} to {targetCoord}.";
                    _lastInteractionStatus = message;
                    AddCombatLogEntry(message);
                    RefreshPresenter();
                    MaybeRunAiTurn();
                    TutorialAttackAccepted();
                    return true;
                }

                var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var attackerIsRanged = IsRangedAttacker(CurrentBattleState, PlayerId.Player, attackerCoord);
                var attacker = CurrentBattleState.GetBoard(PlayerId.Player).GetOccupant(attackerCoord);
                var shielderInfo = ShielderService.ResolveForNormalAttack(
                    CurrentBattleState.GetOpponentBoard(PlayerId.Player),
                    targetCoord,
                    attacker);
                var defenderCounterattacks = CanDefenderCounterattack(
                    CurrentBattleState,
                    PlayerId.Player,
                    attackerCoord,
                    PlayerId.AI,
                    shielderInfo);
                _battleGateway.ExecuteCommand(PlayerId.Player, attackCommand);
                if (CurrentBattleState.LastAttackResolution?.WasCancelledByTrap == true)
                {
                    message = "트랩이 발동해 공격이 취소되었습니다.";
                    _lastInteractionStatus = message;
                    AddCombatLogEntry(message);
                    var trapImpact = BuildCancelledTrapImpact(beforeSnapshot);
                    if (HasPresentationImpact(trapImpact))
                    {
                        QueueLocalTrapReveals();
                        StartPresentationSequence(PlayerSpellImpactSequence(trapImpact));
                    }
                    else
                    {
                        RefreshPresenter();
                        MaybeRunAiTurn();
                    }
                    return true;
                }
                var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                var animationRequest = BuildAttackAnimationRequest(beforeSnapshot, afterSnapshot, PlayerId.Player, attackerCoord, PlayerId.AI, targetCoord, shielderInfo, attackerIsRanged, defenderCounterattacks, valuePopupEvents);

                message = $"Attacked from {attackerCoord} to {targetCoord}.";
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                StartPresentationSequence(PlayerAttackSequence(animationRequest));
                TutorialAttackAccepted();
                return true;
            }
            catch (Exception ex)
            {
                if (_tutorialBattleEntry) ShowCommandErrorToast(ex.Message);
                TutorialAttackRejected(attackerCoord, targetCoord);
                message = ex.Message;
                _lastInteractionStatus = message;
                Debug.LogWarning($"Attack interaction failed from {attackerCoord} to {targetCoord}: {ex.Message}");
                return false;
            }
        }

        private ImpactAnimationRequest BuildCancelledTrapImpact(BattlePresentationSnapshot beforeSnapshot)
        {
            // Return has no combat reaction. Reflection and destruction retain their real
            // damage/removal chains while skipping the canceled attack motion.
            foreach (var trapEvent in CurrentBattleState.TrapEvents)
            {
                if (trapEvent.Kind == BattleTrapEventKind.Triggered &&
                    (trapEvent.CardId == TrapMirrorFieldCardRules.CardId ||
                     IsDirectDestructionTrapCard(trapEvent.CardId)))
                {
                    var request = BuildImpactAnimationRequest(beforeSnapshot, CaptureBattleSnapshot(CurrentBattleState),
                        CloneValuePopupEvents(CurrentBattleState), trapEvent.CardId, trapEvent.OwnerId);
                    if (trapEvent.TargetCoord.HasValue)
                        ConfigureDestructionTrapCue(request, beforeSnapshot, trapEvent.CardId, trapEvent.OwnerId,
                            trapEvent.TargetOwnerId, trapEvent.TargetCoord.Value, trapEvent.TargetRuntimeId);
                    return request;
                }
            }
            return null;
        }
        public bool TryUsePlayerCardOnTile(string cardId, PlayerId targetOwnerId, TileCoord targetCoord, out string message)
        {
            return TryUsePlayerCardOnTile(
                cardId,
                handCardRuntimeId: null,
                targetOwnerId,
                targetCoord,
                out message);
        }

        public bool TryUsePlayerCardOnTile(
            string cardId,
            string handCardRuntimeId,
            PlayerId targetOwnerId,
            TileCoord targetCoord,
            out string message)
        {
            EnsureBattleStarted();
            if (!TutorialAllowsCardUse(cardId, targetOwnerId, targetCoord))
                return FailPlayerCardUseSilently("초록색으로 표시한 실습 대상을 이용하세요.", out message);

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("카드가 선택되지 않았습니다.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                ImpactAnimationRequest summonTrapImpact = null;
                string scriptedSpellLog = null;

                switch (definition.CardType)
                {
                    case CardType.Unit:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' must be placed on a player field tile.", out message);
                        }

                        var playUnitCommand = new PlayUnitCardCommand(
                            cardId,
                            targetCoord,
                            handCardRuntimeId);
                        if (ShouldRoutePlayerActionsOnline())
                        {
                            return TrySendOnlinePlayerCommand(
                                playUnitCommand,
                                $"Sent online play '{cardId}' to {targetCoord}.",
                                out message);
                        }

                        var beforeSummon = CaptureBattleSnapshot(CurrentBattleState);
                        _battleGateway.ExecuteCommand(PlayerId.Player, playUnitCommand);
                        summonTrapImpact = BuildLocalSummonTrapImpact(beforeSummon);
                        break;

                    case CardType.Building:
                        if (targetOwnerId != PlayerId.Player)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' must be placed on a player field tile.", out message);
                        }

                        var playBuildingCommand = new PlayBuildingCardCommand(
                            cardId,
                            targetCoord,
                            handCardRuntimeId);
                        if (ShouldRoutePlayerActionsOnline())
                        {
                            return TrySendOnlinePlayerCommand(
                                playBuildingCommand,
                                $"Sent online play '{cardId}' to {targetCoord}.",
                                out message);
                        }

                        _battleGateway.ExecuteCommand(PlayerId.Player, playBuildingCommand);
                        break;

                    case CardType.Spell:
                        if (definition.HasTrap)
                            return TryCastPlayerPersistentSpell(cardId, handCardRuntimeId, out message);
                        if (definition is DamageSpellCardDefinition)
                        {
                            var castDamageCommand = new CastDamageSpellCommand(
                                cardId,
                                targetOwnerId,
                                targetCoord,
                                handCardRuntimeId);
                            if (ShouldRoutePlayerActionsOnline())
                            {
                                return TrySendOnlinePlayerCommand(
                                    castDamageCommand,
                                    $"Sent online cast '{cardId}' on {targetOwnerId} {targetCoord}.",
                                    out message);
                            }

                            if (!ShouldAnimateAttackSequences())
                            {
                                _battleGateway.ExecuteCommand(PlayerId.Player, castDamageCommand);
                                break;
                            }

                            var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                            _battleGateway.ExecuteCommand(PlayerId.Player, castDamageCommand);
                            var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                            var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState), cardId);

                            message = $"Cast '{cardId}' on {targetOwnerId} {targetCoord}.";
                            _lastInteractionStatus = message;
                            AddCombatLogEntry(message);
                            StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                            TutorialCardUsed(cardId);
                            return true;
                        }

                        if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition)
                        {
                            if (scriptedSpellDefinition.EffectId == EarthquakeRules.EffectId ||
                                scriptedSpellDefinition.EffectId == MeteorRules.EffectId)
                            {
                                if (targetOwnerId != PlayerId.AI || targetCoord.Column < 0 || targetCoord.Column >= BoardState.ColumnCount ||
                                    targetCoord.Row < 0 || targetCoord.Row >= BoardState.RowCount)
                                    return FailPlayerCardUseSilently("해당 위치에 카드를 사용할 수 없습니다.", out message);
                                return TryCastPlayerPersistentSpell(cardId, handCardRuntimeId, out message);
                            }
                            if (string.Equals(scriptedSpellDefinition.EffectId, "cheonra_jimang", StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, "firewall", StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, FireSpearRules.EffectId, StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, PlasmaShieldRules.EffectId, StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, ManaShieldRules.EffectId, StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, BiochemicalBombRules.EffectId, StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, GuRules.EffectId, StringComparison.Ordinal) ||
                                string.Equals(scriptedSpellDefinition.EffectId, HuanShuRules.EffectId, StringComparison.Ordinal))
                            {
                                var castScriptedTargetCommand = new CastScriptedSpellCommand(
                                    cardId,
                                    targetOwnerId,
                                    targetCoord,
                                    handCardRuntimeId);
                                if (ShouldRoutePlayerActionsOnline())
                                {
                                    return TrySendOnlinePlayerCommand(
                                        castScriptedTargetCommand,
                                        $"Sent online cast '{cardId}' on {targetOwnerId} {targetCoord}.",
                                        out message);
                                }

                                var beforeTargetedCast = (scriptedSpellDefinition.EffectId == FirewallSpellEffectId ||
                                    scriptedSpellDefinition.EffectId == FireSpearRules.EffectId ||
                                    scriptedSpellDefinition.EffectId == PlasmaShieldRules.EffectId ||
                                    scriptedSpellDefinition.EffectId == ManaShieldRules.EffectId) && ShouldAnimateAttackSequences()
                                    ? CaptureBattleSnapshot(CurrentBattleState) : null;
                                // Resolve the recipient before execution; append only after a successful cast.
                                scriptedSpellLog = CombatLogFormatter.FormatCommand(PlayerId.Player,
                                    castScriptedTargetCommand, CurrentBattleState, ResolveCombatLogCardDisplayName);
                                _battleGateway.ExecuteCommand(PlayerId.Player, castScriptedTargetCommand);
                                if (beforeTargetedCast != null)
                                {
                                    IReadOnlyList<BattleValuePopupEvent> popups = scriptedSpellDefinition.EffectId == FireSpearRules.EffectId
                                        ? CloneValuePopupEvents(CurrentBattleState) : Array.Empty<BattleValuePopupEvent>();
                                    var impactRequest = BuildImpactAnimationRequest(beforeTargetedCast, CaptureBattleSnapshot(CurrentBattleState),
                                        popups, cardId, PlayerId.Player);
                                    if (scriptedSpellDefinition.EffectId == FireSpearRules.EffectId)
                                        ConfigureFireSpearCast(impactRequest, targetOwnerId, targetCoord);
                                    else if (scriptedSpellDefinition.EffectId == PlasmaShieldRules.EffectId)
                                        ConfigurePlasmaShieldCast(impactRequest, targetOwnerId, targetCoord,
                                            CurrentBattleState.GetBoard(targetOwnerId).GetOccupant(targetCoord)?.RuntimeId);
                                    else if (scriptedSpellDefinition.EffectId == ManaShieldRules.EffectId)
                                        ConfigureManaShieldCast(impactRequest, targetOwnerId, targetCoord,
                                            CurrentBattleState.GetBoard(targetOwnerId).GetOccupant(targetCoord)?.RuntimeId);
                                    else ConfigureFirewallCast(impactRequest, targetOwnerId, targetCoord);
                                    message = scriptedSpellLog;
                                    _lastInteractionStatus = message; AddCombatLogEntry(message);
                                    StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                                    return true;
                                }
                                break;
                            }

                            return FailPlayerCardUseSilently($"Card '{cardId}' is a self-cast scripted spell. Drag it upward to cast it.", out message);
                        }

                        if (definition is PersistentResourceSpellCardDefinition)
                        {
                            return FailPlayerCardUseSilently($"Card '{cardId}' is a persistent spell. Drag it upward to cast it.", out message);
                        }

                        return FailPlayerCardUse($"Card '{cardId}' uses an unsupported spell type in the current slice.", out message);

                    default:
                        return FailPlayerCardUse($"Card '{cardId}' cannot be used on a board tile in the current slice.", out message);
                }

                message = scriptedSpellLog ?? (definition.HasTrap ? "트랩을 설치했습니다." : definition.CardType == CardType.Spell
                    ? $"Cast '{cardId}' on {targetOwnerId} {targetCoord}."
                    : $"Played '{cardId}' to {targetCoord}.");
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                if (summonTrapImpact != null)
                {
                    QueueLocalTrapReveals();
                    StartPresentationSequence(PlayerSpellImpactSequence(summonTrapImpact));
                    return true;
                }
                RefreshPresenter();
                TutorialCardUsed(cardId);
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Play card interaction failed for '{cardId}' at {targetCoord}: {ex.Message}");
                return false;
            }
        }

        private ImpactAnimationRequest BuildLocalSummonTrapImpact(BattlePresentationSnapshot before)
        {
            var battle = CurrentBattleState;
            var summoned = battle?.LastSummonedOccupant;
            if (before == null || summoned == null) return null;
            foreach (var entry in battle.TrapEvents)
            {
                if (entry.Kind != BattleTrapEventKind.Triggered || entry.CardId != SummonTrapCardRules.CardId ||
                    entry.TargetRuntimeId != summoned.RuntimeId) continue;
                before.Occupants.Add(new OccupantPresentationSnapshot(summoned.RuntimeId, summoned.OwnerId,
                    summoned.Position, summoned.CurrentHp, summoned.CardId, false, summoned.Kind));
                var request = BuildImpactAnimationRequest(before, CaptureBattleSnapshot(battle),
                    CloneValuePopupEvents(battle), SummonTrapCardRules.CardId);
                var visual = summoned.CloneDetached();
                // The view is detached from the board; preserve resource/pack attack bonuses captured at summon.
                visual.IncreaseBaseAttack(Math.Max(0, battle.LastSummonedAttack - visual.Attack));
                request.SummonedVisual = visual;
                return request;
            }
            return null;
        }

        private OccupantState CreateOnlineSummonTrapVisual(BattleEventDto trigger, IReadOnlyList<BattleEventDto> events)
        {
            foreach (var entry in events)
            {
                if (entry.EventType != BattleEventType.CardPlayed || entry.SourceOwnerId != trigger.TargetOwnerId ||
                    entry.RuntimeId != trigger.TargetRuntimeId || entry.CardMaxHp <= 0 ||
                    !(_cardDefinitionProvider?.GetRequired(entry.CardId) is UnitCardDefinition definition)) continue;
                var hp = entry.CardMaxHp;
                foreach (var damage in events)
                    if ((damage.EventType == BattleEventType.DamageApplied || damage.EventType == BattleEventType.InvinciblePrevented) &&
                        damage.TargetRuntimeId == trigger.TargetRuntimeId && damage.HpBefore > 0) { hp = damage.HpBefore; break; }
                var visual = new UnitState(entry.RuntimeId, entry.CardId, entry.SourceOwnerId,
                    trigger.TargetCoord.ToDomain(), definition.AttackType, entry.CardAttack, entry.CardMaxHp,
                    definition.CanMove, definition.IsScience, definition.SciencePowerUpkeep,
                    turnStartResourceGain: definition.TurnStartResourceGain,
                    maxAttacksPerTurn: definition.MaxAttacksPerTurn, hitsPerAttack: definition.HitsPerAttack,
                    hasBerserker: definition.HasBerserker, hasEndure: definition.HasEndure,
                    hasShielder: definition.HasShielder, hasLifeSteal: definition.HasLifeSteal,
                    damageType: definition.DamageType, physicalDefense: definition.PhysicalDefense,
                    magicDefense: definition.MagicDefense, races: definition.Races, hasRush: definition.HasRush,
                    hasHiding: definition.HasHiding, hasFlying: definition.HasFlying,
                    spellPower: definition.SpellPower, hasPiercing: definition.HasPiercing);
                visual.WasSummonedThisTurn = true;
                visual.HasSummoningSickness = !definition.HasRush;
                if (definition.SealboundOwnerTurnStarts > 0) visual.EnterSealbound(definition.SealboundOwnerTurnStarts);
                visual.AddInvincibleEffect(definition.InvincibleDuration, definition.InvincibleOwnerTurns,
                    CurrentBattleState?.TurnNumber ?? 0, CurrentBattleState?.ActivePlayerId);
                visual.CurrentHp = hp;
                return visual;
            }
            return null;
        }

        private bool PresentSummonTrapVisual(OccupantState summoned)
        {
            var text = FindTileTextView(summoned.OwnerId, summoned.Position);
            var tile = text == null ? null : text.GetComponent<TileView>();
            if (tile == null) return false;
            // A newer state may already have replaced this exact occupant.
            if (tile.CurrentOccupant != null && tile.CurrentOccupant.RuntimeId != summoned.RuntimeId) return false;
            tile.Present(summoned);
            text.SetTile(tile.Title, tile.Content, true);
            if (!_summonTrapVisualTiles.Contains(tile)) _summonTrapVisualTiles.Add(tile);
            return true;
        }

        private void RestoreSummonTrapVisuals()
        {
            foreach (var tile in _summonTrapVisualTiles)
                if (tile != null)
                {
                    tile.Present(CurrentBattleState?.GetBoard(tile.OwnerId).GetOccupant(tile.Coord));
                    tile.GetComponent<TileTextView>()?.SetTile(tile.Title, tile.Content, tile.IsOccupied);
                }
            _summonTrapVisualTiles.Clear();
        }

        private bool HasPlayerArmedTrap(string cardId)
        {
            var battle = CurrentBattleState;
            if (battle == null) return false;
            foreach (var trap in battle.ArmedTraps)
                if (trap.OwnerId == PlayerId.Player && string.Equals(trap.CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool TryCastPlayerPersistentSpell(string cardId, out string message)
        {
            return TryCastPlayerPersistentSpell(cardId, handCardRuntimeId: null, out message);
        }

        public bool TryCastPlayerPersistentSpell(
            string cardId,
            string handCardRuntimeId,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("카드가 선택되지 않았습니다.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId);
                if (definition.HasTrap)
                {
                    _battleScreenPresenter?.TutorialHand?.PrepareTrapCardUse(cardId, handCardRuntimeId);
                    var installCommand = new InstallTrapCommand(cardId, handCardRuntimeId);
                    if (ShouldRoutePlayerActionsOnline())
                        return TrySendOnlinePlayerCommand(installCommand, "트랩을 설치했습니다.", out message);
                    _battleGateway.ExecuteCommand(PlayerId.Player, installCommand);
                    _battleScreenPresenter?.TutorialHand?.ConfirmSpellCardUse(cardId, handCardRuntimeId);
                    message = "트랩을 설치했습니다.";
                }
                else if (definition is PersistentResourceSpellCardDefinition)
                {
                    _battleScreenPresenter?.TutorialHand?.PrepareSpellCardUse(cardId, handCardRuntimeId);
                    var castPersistentCommand = new CastPersistentResourceSpellCommand(
                        cardId,
                        handCardRuntimeId);
                    if (ShouldRoutePlayerActionsOnline())
                    {
                        return TrySendOnlinePlayerCommand(
                            castPersistentCommand,
                            $"Sent online cast persistent spell '{cardId}'.",
                            out message);
                    }

                    _battleGateway.ExecuteCommand(PlayerId.Player, castPersistentCommand);
                    _battleScreenPresenter?.TutorialHand?.ConfirmSpellCardUse(cardId, handCardRuntimeId);
                    message = $"Cast persistent spell '{cardId}'.";
                }
                else if (definition is ScriptedSpellCardDefinition scriptedSpellDefinition &&
                         (string.Equals(scriptedSpellDefinition.EffectId, "daehwandan", StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, TimedBombRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, NuclearRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, EarthquakeRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, MeteorRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, MagicAmplificationCircleRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, PowerBankRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, ManaStoneRules.EffectId, StringComparison.Ordinal) ||
                          string.Equals(scriptedSpellDefinition.EffectId, ManaStoneBundleRules.EffectId, StringComparison.Ordinal)))
                {
                    _battleScreenPresenter?.TutorialHand?.PrepareSpellCardUse(cardId, handCardRuntimeId);
                    var castScriptedCommand = new CastScriptedSpellCommand(
                        cardId,
                        handCardRuntimeId);
                    if (ShouldRoutePlayerActionsOnline())
                    {
                        return TrySendOnlinePlayerCommand(
                            castScriptedCommand,
                            $"Sent online cast spell '{cardId}'.",
                            out message);
                    }

                    var beforeAreaCast = (scriptedSpellDefinition.EffectId == NuclearRules.EffectId || scriptedSpellDefinition.EffectId == EarthquakeRules.EffectId ||
                                          scriptedSpellDefinition.EffectId == MeteorRules.EffectId) && ShouldAnimateAttackSequences()
                        ? CaptureBattleSnapshot(CurrentBattleState) : null;
                    _battleGateway.ExecuteCommand(PlayerId.Player, castScriptedCommand);
                    _battleScreenPresenter?.TutorialHand?.ConfirmSpellCardUse(cardId, handCardRuntimeId);
                    message = CombatLogFormatter.FormatCommand(PlayerId.Player, castScriptedCommand,
                        CurrentBattleState, ResolveCombatLogCardDisplayName);
                    if (beforeAreaCast != null)
                    {
                        var impactRequest = BuildImpactAnimationRequest(beforeAreaCast,
                            CaptureBattleSnapshot(CurrentBattleState), CloneValuePopupEvents(CurrentBattleState), cardId, PlayerId.Player);
                        _lastInteractionStatus = message;
                        AddCombatLogEntry(message);
                        StartPresentationSequence(PlayerSpellImpactSequence(impactRequest));
                        return true;
                    }
                }
                else
                {
                    return FailPlayerCardUse("사용 가능한 대상이 아닙니다.", out message);
                }

                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                RefreshPresenter();
                MaybeRunAiTurn();
                return true;
            }
            catch (Exception ex)
            {
                CancelPendingSpellCardUse();
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Cast persistent spell interaction failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        public bool TryBeginPlayerRobotFusion(string cardId, out string message)
        {
            return TryBeginPlayerRobotFusion(cardId, handCardRuntimeId: null, out message);
        }

        public bool TryBeginPlayerRobotFusion(
            string cardId,
            string handCardRuntimeId,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return FailPlayerCardUse("카드가 선택되지 않았습니다.", out message);
            }

            try
            {
                var definition = _cardDefinitionProvider.GetRequired(cardId) as ScriptedSpellCardDefinition;
                if (definition == null ||
                    !string.Equals(definition.EffectId, RobotFusionRules.EffectId, StringComparison.Ordinal))
                {
                    return FailPlayerCardUse($"Card '{cardId}' is not Robot Fusion.", out message);
                }

                if (!RobotFusionRules.CanBegin(CurrentBattleState, PlayerId.Player))
                {
                    return FailPlayerCardUse("살아 있는 아군 로봇 유닛이 2기 이상 필요합니다.", out message);
                }

                var command = new CastScriptedSpellCommand(cardId, handCardRuntimeId);
                if (ShouldRoutePlayerActionsOnline())
                {
                    return TrySendOnlinePlayerCommand(
                        command,
                        "로봇 합체 대상을 선택하세요.",
                        out message);
                }

                _battleGateway.ExecuteCommand(PlayerId.Player, command);
                message = "로봇 합체 대상을 선택하세요.";
                _lastInteractionStatus = message;
                RefreshPresenter();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Begin Robot Fusion failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        public bool TryResolvePlayerRobotFusion(
            string cardId,
            IReadOnlyList<TileCoord> selectedCoords,
            out string message)
        {
            EnsureBattleStarted();

            if (IsBusy)
            {
                return FailPlayerCardUse(BusyMessage, out message);
            }

            if (selectedCoords == null || selectedCoords.Count < RobotFusionRules.MinimumRobotCount)
            {
                return FailPlayerCardUse("합체할 살아 있는 아군 로봇 유닛을 2기 이상 선택하세요.", out message);
            }

            try
            {
                var command = new CastScriptedSpellCommand(cardId, selectedCoords);
                if (ShouldRoutePlayerActionsOnline())
                {
                    return TrySendOnlinePlayerCommand(command, "로봇 합체 명령을 전송했습니다.", out message);
                }

                _battleGateway.ExecuteCommand(PlayerId.Player, command);
                message = "로봇 합체가 완료되었습니다.";
                _lastInteractionStatus = message;
                RefreshPresenter();
                return true;
            }
            catch (Exception ex)
            {
                FailPlayerCardUse(ex.Message, out message);
                Debug.LogWarning($"Resolve Robot Fusion failed for '{cardId}': {ex.Message}");
                return false;
            }
        }

        private BattleState _lastLocalDrawBattleState;
        private int _lastLocalDrawEventCount;

        private void QueueLocalCardDrawAnimations()
        {
            if (_sendPlayerActionsToOnlineGateway) return;
            var state = CurrentBattleState;
            if (!ReferenceEquals(state, _lastLocalDrawBattleState))
            {
                _lastLocalDrawBattleState = state;
                _lastLocalDrawEventCount = state?.CardDrawEvents.Count ?? 0;
                return;
            }
            if (state == null) return;
            var count = 0;
            for (var i = _lastLocalDrawEventCount; i < state.CardDrawEvents.Count; i++)
                if (state.CardDrawEvents[i].OwnerId == PlayerId.Player) count++;
            _lastLocalDrawEventCount = state.CardDrawEvents.Count;
            _battleScreenPresenter?.QueueCardDrawAnimations(count);
        }

        private void RefreshPresenter()
        {
            QueueLocalTrapReveals();
            QueueLocalCardDrawAnimations();
            _battleScreenPresenter?.Present(CurrentBattleState);

            if (ShouldQueueDraftSceneReturn())
            {
                StartCoroutine(ReturnToDraftSceneAfterDelay());
            }
        }

        private BattleState GetCurrentBattleState()
        {
            if (_sendPlayerActionsToOnlineGateway &&
                _onlineConnectionTester != null &&
                _onlineConnectionTester.CurrentProjectedBattleState != null)
            {
                return _onlineConnectionTester.CurrentProjectedBattleState;
            }

            return _battleGateway?.CurrentBattleState;
        }

        private bool ShouldRoutePlayerActionsOnline()
        {
            if (!_sendPlayerActionsToOnlineGateway)
            {
                return false;
            }

            AutoAssignOnlineConnectionTester();
            return _onlineConnectionTester != null;
        }

        private bool TrySendOnlinePlayerCommand(IBattleCommand command, string successMessage, out string message)
        {
            if (!ShouldRoutePlayerActionsOnline())
            {
                message = string.Empty;
                return false;
            }

            if (_onlineConnectionTester.TrySendPlayerCommand(command, out var sendMessage))
            {
                message = string.IsNullOrWhiteSpace(successMessage) ? sendMessage : successMessage;
                _lastInteractionStatus = message;
                AddCombatLogEntry(message);
                return true;
            }

            message = sendMessage;
            CancelPendingSpellCardUse();
            ShowCommandErrorToast(message);
            return false;
        }

        private bool FailPlayerCardUse(string failureMessage, out string message)
        {
            message = string.IsNullOrWhiteSpace(failureMessage)
                ? "카드 사용이 실패했습니다."
                : failureMessage;
            ShowCommandErrorToast(message);
            return false;
        }

        private static bool FailPlayerCardUseSilently(string failureMessage, out string message)
        {
            message = string.IsNullOrWhiteSpace(failureMessage)
                ? "카드 사용이 실패했습니다."
                : failureMessage;
            return false;
        }

        private void AutoAssignOnlineConnectionTester()
        {
            if (_onlineConnectionTester != null || !_sendPlayerActionsToOnlineGateway)
            {
                return;
            }

            var testers = UnityEngine.Object.FindObjectsByType<OnlineBattleConnectionTester>(FindObjectsSortMode.None);
            foreach (var tester in testers)
            {
                if (tester != null && tester.PresentsStateViewsToBattleScreen)
                {
                    _onlineConnectionTester = tester;
                    return;
                }
            }

            if (testers.Length > 0)
            {
                _onlineConnectionTester = testers[0];
            }
        }

        private OnlineBattleConnectionTester ResolveOrCreateOnlineConnectionTester()
        {
            AutoAssignOnlineConnectionTester();
            if (_onlineConnectionTester != null)
            {
                return _onlineConnectionTester;
            }

            var testerObject = new GameObject("OnlineBattleConnectionTester_Player");
            testerObject.transform.SetParent(transform, false);
            _onlineConnectionTester = testerObject.AddComponent<OnlineBattleConnectionTester>();
            return _onlineConnectionTester;
        }

        private string BuildOnlineLaunchTrace(
            string modeLabel,
            string matchId,
            IReadOnlyList<string> playerDeckCardIds)
        {
            return
                $"mode={modeLabel} match={FormatLogValue(matchId)} server={FormatLogValue(OnlineServerUrl)} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} runStatus={FormatLogValue(AccountSessionState.ActiveRunStatus)} record={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} deckCards={playerDeckCardIds?.Count ?? 0} serverDeckCards={AccountSessionState.ActiveDeckCardCount} upgradedCardKinds={CountUpgradedCardKinds(playerDeckCardIds)}";
        }

        private static string FormatShortRunDeckTrace()
        {
            return $"run={FormatLogValue(AccountSessionState.ActiveRunId)}, deck={FormatLogValue(AccountSessionState.ActiveDeckId)}";
        }

        private static string FormatLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static int CountUpgradedCardKinds(IReadOnlyList<string> cardIds)
        {
            if (cardIds == null || cardIds.Count == 0)
            {
                return 0;
            }

            var upgradedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cardIds.Count; i++)
            {
                var cardId = cardIds[i];
                if (!string.IsNullOrWhiteSpace(cardId) &&
                    AccountSessionState.GetOwnedCardUpgradeLevel(cardId) > 0)
                {
                    upgradedCardIds.Add(cardId);
                }
            }

            return upgradedCardIds.Count;
        }

        private string CreateOnlineDraftMatchId()
        {
            var prefix = string.IsNullOrWhiteSpace(_onlineMatchIdPrefix)
                ? "draft-run"
                : _onlineMatchIdPrefix.Trim();
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{suffix}";
        }

        private string CreateOnlineMatchmakingPlayerToken()
        {
            var baseToken = string.IsNullOrWhiteSpace(_onlinePlayerToken)
                ? "player"
                : _onlinePlayerToken.Trim();
            return $"{baseToken}-{OnlineClientTokenSuffix}";
        }

        private void MaybeRunAiTurn()
        {
            if (!_autoRunAiTurns || CurrentBattleState == null || CurrentBattleState.IsEnded || CurrentBattleState.ActivePlayerId != PlayerId.AI)
            {
                return;
            }

            if (ShouldAnimateAttackSequences() && _aiDecisionService != null)
            {
                StartPresentationSequence(RunAiTurnSequence());
                return;
            }

            if (_aiTurnRunner == null)
            {
                return;
            }

            var beforeAiMovement = _battleScreenPresenter != null ? CaptureBattleSnapshot(CurrentBattleState) : null;
            _aiTurnRunner.RunAiTurn(_battleGateway, AddCombatLogEntry, ResolveCombatLogCardDisplayName);
            // The non-animated debug runner completes its actions in one synchronous batch.
            if (beforeAiMovement != null)
            {
                var afterAiMovement = CaptureBattleSnapshot(CurrentBattleState);
                foreach (var before in beforeAiMovement.Occupants)
                {
                    var after = afterAiMovement.FindByRuntimeId(before.RuntimeId);
                    if (after == null || after.OwnerId != before.OwnerId || after.Coord == before.Coord) continue;
                    _battleScreenPresenter.PlayUnitMoveSound();
                    break;
                }
            }


            if (_autoResolvePlayerTurnStartAfterAi &&
                CurrentBattleState != null &&
                !CurrentBattleState.IsEnded &&
                CurrentBattleState.ActivePlayerId == PlayerId.Player &&
                CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                _battleGateway.ResolveTurnStart();
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
            }

            RefreshPresenter();
        }

        private void ClearCombatLog()
        {
            _combatLogEntries.Clear();
            _combatLogText = "전투 기록이 없습니다.";
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private void ReplaceCombatLogEntries(IReadOnlyList<BattleCombatLogEntryDto> entries)
        {
            _combatLogEntries.Clear();
            if (entries != null)
            {
                var firstIndex = Math.Max(0, entries.Count - MaxCombatLogEntries);
                for (var i = firstIndex; i < entries.Count; i++)
                {
                    var formattedEntry = BattleCombatLogEntryFormatter.Format(
                        entries[i],
                        ResolveCombatLogCardDisplayName);
                    if (!string.IsNullOrWhiteSpace(formattedEntry))
                    {
                        _combatLogEntries.Add(formattedEntry);
                    }
                }
            }

            _combatLogText = _combatLogEntries.Count == 0
                ? "전투 기록이 없습니다."
                : string.Join(Environment.NewLine, _combatLogEntries);
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private string ResolveCombatLogCardDisplayName(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return "카드";
            }

            if (string.Equals(cardId, "master", StringComparison.OrdinalIgnoreCase))
            {
                return "마스터";
            }

            if (TryGetCardDefinition(cardId, out var definition) &&
                !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            if (TryGetCardDefinitionAsset(cardId, out var cardAsset) &&
                !string.IsNullOrWhiteSpace(cardAsset.DisplayName))
            {
                return cardAsset.DisplayName;
            }

            return cardId;
        }

        private void AddCombatLogEntry(string entry)
        {
            if (_tutorialBattleEntry) { RecordTutorialCombatLog(); return; }
            AppendCombatLogText(entry);
        }
        private void AppendCombatLogText(string entry)
        {
            // Online PVE/PVP logs come from the authoritative server StateView.
            if (_sendPlayerActionsToOnlineGateway)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            _combatLogEntries.Add(entry);
            while (_combatLogEntries.Count > MaxCombatLogEntries)
            {
                _combatLogEntries.RemoveAt(0);
            }

            _combatLogText = string.Join(Environment.NewLine, _combatLogEntries);
            _onCombatLogChanged.Invoke(_combatLogText);
        }

        private bool TryBeginImmediateInteraction()
        {
            if (_tutorialBattleEntry) return false;
            if (!IsBusy)
            {
                return true;
            }

            _lastInteractionStatus = BusyMessage;
            return false;
        }

        private void EnsureBattleStarted()
        {
            if (ShouldRoutePlayerActionsOnline())
            {
                return;
            }

            if (_battleGateway == null || CurrentBattleState == null)
            {
                throw new InvalidOperationException("Battle must be started before scene actions can be used.");
            }
        }

        private BattleBootstrapperResolvedConfig ResolveStartConfig()
        {
            var resolvedConfig = BattleBootstrapperConfigResolver.Resolve(
                _cardCatalogAsset,
                _playerDeckAsset,
                _aiDeckAsset,
                _playerDeckCardIds,
                _aiDeckCardIds,
                _useGeneratedDebugDecksWhenEmpty,
                _generatedDeckSize,
                _runtimePlayerDeckCardIdsOverride,
                _runtimeAIDeckCardIdsOverride,
                _useJsonCardDefinitions);

            if (resolvedConfig.PlayerDeckCardIds == null || resolvedConfig.PlayerDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("Player deck card ids must be configured before starting the battle.");
            }

            if (resolvedConfig.AIDeckCardIds == null || resolvedConfig.AIDeckCardIds.Count == 0)
            {
                throw new InvalidOperationException("AI deck card ids must be configured before starting the battle.");
            }

            return resolvedConfig;
        }

        private static BattleFlowController CreateBattleFlowController(ICardDefinitionProvider cardDefinitionProvider)
        {
            var provider = cardDefinitionProvider ?? new InMemoryCardDefinitionProvider(Array.Empty<CardDefinition>());
            var processor = new BattleCommandProcessor(
                new PlayCardService(provider),
                new SpellService(provider),
                new MoveService(),
                new AttackService(),
                new EndTurnService());

            return new BattleFlowController(
                new BattleSetupService(),
                new MulliganService(),
                new TurnStartService(provider),
                processor);
        }

        private bool ShouldAnimateAttackSequences()
        {
            return _animateAttackSequences &&
                   UnityEngine.Application.isPlaying &&
                   _battleScreenPresenter != null &&
                   _battleScreenPresenter.BoardPresenter != null;
        }

        private DraftOverlayPresenter ResolveDraftOverlayPresenter()
        {
            if (_draftOverlayPresenter != null)
            {
                _draftOverlayPresenter.Bind(this);
                return _draftOverlayPresenter;
            }

            _draftOverlayPresenter = GetComponent<DraftOverlayPresenter>();
            if (_draftOverlayPresenter == null)
            {
                var presenterObject = new GameObject("DraftOverlayPresenter");
                _draftOverlayPresenter = presenterObject.AddComponent<DraftOverlayPresenter>();
            }

            _draftOverlayPresenter.Bind(this);
            return _draftOverlayPresenter;
        }

        private System.Random CreateDraftRandom()
        {
            return _useFixedDraftSeed
                ? new System.Random(_fixedDraftSeed)
                : new System.Random();
        }

        private bool ShouldQueueDraftSceneReturn()
        {
            return !_hasQueuedDraftSceneReturn &&
                   _returnToDraftSceneWhenRunSessionActive &&
                   HasDraftRunContextForSceneReturn() &&
                   CurrentBattleState != null &&
                   CurrentBattleState.IsEnded &&
                   !string.IsNullOrWhiteSpace(DraftRunSessionState.DraftSceneName);
        }

        private static bool HasDraftRunContextForSceneReturn()
        {
            return DraftRunSessionState.HasDraftedDeckReady ||
                   AccountSessionState.HasSavedDraftDeck;
        }

        private IEnumerator ReturnToDraftSceneAfterDelay()
        {
            _hasQueuedDraftSceneReturn = true;

            // Let the lethal attack/death presentation finish before the result's hold time.
            // Online projection can replace the state object with an equivalent final snapshot.
            while ((IsBusy || IsOpponentPlayedCardRevealPending) && CurrentBattleState != null && CurrentBattleState.IsEnded)
            {
                yield return null;
            }

            if (CurrentBattleState == null || !CurrentBattleState.IsEnded)
            {
                _hasQueuedDraftSceneReturn = false;
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(
                _returnToDraftSceneDelaySeconds, BattleOutcomeBannerView.MinimumDisplaySeconds));

            if (CurrentBattleState == null || !CurrentBattleState.IsEnded)
            {
                _hasQueuedDraftSceneReturn = false;
                yield break;
            }

            var isDraw = CurrentBattleState.Result.IsDraw;
            var playerWon = CurrentBattleState.Result.HasWinner &&
                            CurrentBattleState.Result.Winner == PlayerId.Player;
            var syncedServerRunRecord = false;
            if (ShouldSyncServerRunRecordBeforeDraftReturn())
            {
                yield return TrySyncServerRunRecordBeforeDraftReturn(
                    succeeded => syncedServerRunRecord = succeeded);
            }

            if (string.Equals(DraftRunSessionState.ActiveBattleGameMode, "ai_challenge", StringComparison.Ordinal))
            {
                // A challenge has its own server receipt; it never changes the current run record.
                DraftRunSessionState.RecordAiChallengeOutcome(playerWon, isDraw);
            }
            else if (isDraw)
            {
                DraftRunSessionState.RecordBattleDraw();
            }
            else if (!syncedServerRunRecord)
            {
                DraftRunSessionState.RecordBattleResult(playerWon);
            }
            else
            {
                DraftRunSessionState.SetLastBattleOutcome(
                    playerWon ? DraftBattleOutcome.Victory : DraftBattleOutcome.Defeat);
            }

            var localResultLabel = isDraw ? "draw" : playerWon ? "win" : "loss";
            Debug.Log(
                $"[After333 Battle Result] returningToDraft localResult={localResultLabel} serverSynced={syncedServerRunRecord} run={FormatLogValue(AccountSessionState.ActiveRunId)} deck={FormatLogValue(AccountSessionState.ActiveDeckId)} localRecord={DraftRunSessionState.Wins}-{DraftRunSessionState.Losses} serverActiveRecord={AccountSessionState.ActiveRunWins}-{AccountSessionState.ActiveRunLosses} latestRecord={AccountSessionState.LatestRunWins}-{AccountSessionState.LatestRunLosses}");
            SceneManager.LoadScene(DraftRunSessionState.DraftSceneName);
        }

        private bool ShouldSyncServerRunRecordBeforeDraftReturn()
        {
            return ShouldRoutePlayerActionsOnline() &&
                   AccountSessionState.IsAuthenticated &&
                   !string.IsNullOrWhiteSpace(AccountSessionState.SessionToken);
        }

        private IEnumerator TrySyncServerRunRecordBeforeDraftReturn(Action<bool> completed)
        {
            using var cancellation = new CancellationTokenSource();
            var syncTask = SyncServerRunRecordBeforeDraftReturnAsync(cancellation.Token);
            var deadline = Time.unscaledTime + ServerRunResultSyncTimeoutSeconds;

            while (!syncTask.IsCompleted && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (!syncTask.IsCompleted)
            {
                cancellation.Cancel();
                while (!syncTask.IsCompleted)
                {
                    yield return null;
                }

                Debug.LogWarning("[After333 Battle Result] server run sync timed out before returning to Draft. Falling back to local result cache.");
                completed?.Invoke(false);
                yield break;
            }

            if (syncTask.IsCanceled)
            {
                completed?.Invoke(false);
                yield break;
            }

            if (syncTask.IsFaulted)
            {
                var message = syncTask.Exception?.GetBaseException().Message ?? "unknown error";
                Debug.LogWarning($"[After333 Battle Result] server run sync failed before returning to Draft: {message}");
                completed?.Invoke(false);
                yield break;
            }

            completed?.Invoke(syncTask.Result);
        }

        private static async Task<bool> SyncServerRunRecordBeforeDraftReturnAsync(CancellationToken cancellationToken)
        {
            var client = new GuestAuthClient(
                Project333.Runtime.Presentation.Project333ServerEndpointSettings.ResolveHttpUrl(null));
            var response = await client.GetMeAsync(AccountSessionState.SessionToken, cancellationToken);
            AccountSessionState.ApplyMeResponse(response);

            if (AccountSessionState.HasResumableRun)
            {
                DraftRunSessionState.ApplyServerRunRecord(
                    AccountSessionState.ActiveRunWins,
                    AccountSessionState.ActiveRunLosses);
                return true;
            }

            if (AccountSessionState.HasLatestRun)
            {
                DraftRunSessionState.ApplyServerRunRecord(
                    AccountSessionState.LatestRunWins,
                    AccountSessionState.LatestRunLosses);
                return true;
            }

            return false;
        }

        private void StartPresentationSequence(IEnumerator sequence)
        {
            if (sequence == null)
            {
                return;
            }

            CancelPresentationSequence();
            _presentationSequenceCoroutine = StartCoroutine(RunPresentationSequence(sequence));
        }

        private void CancelPresentationSequence()
        {
            if (_presentationSequenceCoroutine != null)
            {
                StopCoroutine(_presentationSequenceCoroutine);
                _presentationSequenceCoroutine = null;
            }

            _isBusy = false;
        }

        private IEnumerator RunPresentationSequence(IEnumerator sequence)
        {
            _isBusy = true;
            yield return sequence;
            _presentationSequenceCoroutine = null;
            _isBusy = false;
            RefreshPresenter();
        }

        private IEnumerator PlayerAttackSequence(AttackAnimationRequest animationRequest)
        {
            yield return PlayAttackAnimationSequence(animationRequest);
            RefreshPresenter();

            if (_autoRunAiTurns && CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                yield return RunAiTurnSequence();
            }
        }

        private IEnumerator PlayerSpellImpactSequence(ImpactAnimationRequest impactRequest)
        {
            yield return PlayImpactAnimationSequence(impactRequest);
            RefreshPresenter();

            if (_autoRunAiTurns && CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                yield return RunAiTurnSequence();
            }
        }

        private IEnumerator PresentationOnlySequence(ImpactAnimationRequest impactRequest)
        {
            yield return PlayImpactAnimationSequence(impactRequest);
        }

        private IEnumerator RunAiTurnSequence()
        {
            while (CurrentBattleState != null && !CurrentBattleState.IsEnded && CurrentBattleState.ActivePlayerId == PlayerId.AI)
            {
                if (CurrentBattleState.Phase == PhaseType.TurnStart)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    _battleGateway.ResolveTurnStart();
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.AI, CurrentBattleState.TurnNumber));
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                    if (HasPresentationImpact(impactRequest))
                    {
                        yield return PlayImpactAnimationSequence(impactRequest);
                    }

                    RefreshPresenter();
                    yield return null;
                    continue;
                }

                if (CurrentBattleState.Phase != PhaseType.Main || _aiDecisionService == null)
                {
                    yield break;
                }

                var command = _aiDecisionService.GetNextCommand(CurrentBattleState);
                var actionPresentationStarted = Time.unscaledTime;
                if (command is EndTurnCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    AddCombatLogEntry(CombatLogFormatter.FormatEndTurn(PlayerId.AI));
                    _battleGateway.EndTurn();
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                    if (HasPresentationImpact(impactRequest))
                    {
                        yield return PlayImpactAnimationSequence(impactRequest);
                    }

                    RefreshPresenter();
                    break;
                }

                if (command is AttackCommand attackCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var attackerIsRanged = IsRangedAttacker(CurrentBattleState, PlayerId.AI, attackCommand.AttackerCoord);
                    var attacker = CurrentBattleState.GetBoard(PlayerId.AI).GetOccupant(attackCommand.AttackerCoord);
                    var shielderInfo = ShielderService.ResolveForNormalAttack(
                        CurrentBattleState.GetOpponentBoard(PlayerId.AI),
                        attackCommand.TargetCoord,
                        attacker);
                    var defenderCounterattacks = CanDefenderCounterattack(
                        CurrentBattleState,
                        PlayerId.AI,
                        attackCommand.AttackerCoord,
                        PlayerId.Player,
                        shielderInfo);
                    _battleGateway.ExecuteCommand(PlayerId.AI, attackCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, attackCommand));
                    if (CurrentBattleState.LastAttackResolution?.WasCancelledByTrap == true)
                    {
                        var trapImpact = BuildCancelledTrapImpact(beforeSnapshot);
                        if (HasPresentationImpact(trapImpact))
                        {
                            QueueLocalTrapReveals();
                            yield return PlayImpactAnimationSequence(trapImpact);
                        }
                        RefreshPresenter();
                        yield return PauseAfterLocalAiAction(command, actionPresentationStarted);
                        continue;
                    }
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var valuePopupEvents = CloneValuePopupEvents(CurrentBattleState);
                    var animationRequest = BuildAttackAnimationRequest(beforeSnapshot, afterSnapshot, PlayerId.AI, attackCommand.AttackerCoord, PlayerId.Player, attackCommand.TargetCoord, shielderInfo, attackerIsRanged, defenderCounterattacks, valuePopupEvents);
                    yield return PlayAttackAnimationSequence(animationRequest);
                    RefreshPresenter();
                    yield return PauseAfterLocalAiAction(command, actionPresentationStarted);
                    continue;
                }

                if (command is CastDamageSpellCommand castDamageSpellCommand)
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    _battleGateway.ExecuteCommand(PlayerId.AI, castDamageSpellCommand);
                    AddCombatLogEntry(CombatLogFormatter.FormatCommand(PlayerId.AI, castDamageSpellCommand));
                    var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState), castDamageSpellCommand.CardId);
                    yield return PlayImpactAnimationSequence(impactRequest);
                    RefreshPresenter();
                    yield return PauseAfterLocalAiAction(command, actionPresentationStarted);
                    continue;
                }

                if (command is CastScriptedSpellCommand scriptedCommand &&
                    (scriptedCommand.CardId == NuclearRules.CardId || scriptedCommand.CardId == EarthquakeRules.CardId ||
                     scriptedCommand.CardId == MeteorRules.CardId || (scriptedCommand.HasTarget && (IsFirewallCard(scriptedCommand.CardId) || IsFireSpearCard(scriptedCommand.CardId) || scriptedCommand.CardId == PlasmaShieldRules.CardId || scriptedCommand.CardId == ManaShieldRules.CardId))))
                {
                    var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                    var scriptedSpellLog = CombatLogFormatter.FormatCommand(PlayerId.AI, scriptedCommand,
                        CurrentBattleState, ResolveCombatLogCardDisplayName);
                    _battleGateway.ExecuteCommand(PlayerId.AI, scriptedCommand);
                    AddCombatLogEntry(scriptedSpellLog);
                    var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, CaptureBattleSnapshot(CurrentBattleState),
                        CloneValuePopupEvents(CurrentBattleState), scriptedCommand.CardId, PlayerId.AI);
                    if (IsFirewallCard(scriptedCommand.CardId))
                    {
                        impactRequest.ValuePopupEvents.Clear();
                        ConfigureFirewallCast(impactRequest, scriptedCommand.TargetOwnerId, scriptedCommand.TargetCoord);
                    }
                    if (IsFireSpearCard(scriptedCommand.CardId))
                        ConfigureFireSpearCast(impactRequest, scriptedCommand.TargetOwnerId, scriptedCommand.TargetCoord);
                    if (scriptedCommand.CardId == PlasmaShieldRules.CardId)
                        ConfigurePlasmaShieldCast(impactRequest, scriptedCommand.TargetOwnerId, scriptedCommand.TargetCoord,
                            CurrentBattleState.GetBoard(scriptedCommand.TargetOwnerId).GetOccupant(scriptedCommand.TargetCoord)?.RuntimeId);
                    if (scriptedCommand.CardId == ManaShieldRules.CardId)
                        ConfigureManaShieldCast(impactRequest, scriptedCommand.TargetOwnerId, scriptedCommand.TargetCoord,
                            CurrentBattleState.GetBoard(scriptedCommand.TargetOwnerId).GetOccupant(scriptedCommand.TargetCoord)?.RuntimeId);
                    yield return PlayImpactAnimationSequence(impactRequest);
                    RefreshPresenter();
                    yield return PauseAfterLocalAiAction(command, actionPresentationStarted);
                    continue;
                }

                var beforeSummon = command is PlayUnitCardCommand ? CaptureBattleSnapshot(CurrentBattleState) : null;
                var commandLog = CombatLogFormatter.FormatCommand(PlayerId.AI, command,
                    CurrentBattleState, ResolveCombatLogCardDisplayName);
                _battleGateway.ExecuteCommand(PlayerId.AI, command);
                if (command is MoveOccupantCommand) _battleScreenPresenter?.PlayUnitMoveSound();
                var summonTrapImpact = BuildLocalSummonTrapImpact(beforeSummon);
                if (summonTrapImpact != null)
                {
                    QueueLocalTrapReveals();
                    yield return PlayImpactAnimationSequence(summonTrapImpact);
                }
                AddCombatLogEntry(commandLog);
                RefreshPresenter();
                yield return PauseAfterLocalAiAction(command, actionPresentationStarted);
            }

            if (_autoResolvePlayerTurnStartAfterAi &&
                CurrentBattleState != null &&
                !CurrentBattleState.IsEnded &&
                CurrentBattleState.ActivePlayerId == PlayerId.Player &&
                CurrentBattleState.Phase == PhaseType.TurnStart)
            {
                var beforeSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                _battleGateway.ResolveTurnStart();
                var afterSnapshot = CaptureBattleSnapshot(CurrentBattleState);
                AddCombatLogEntry(CombatLogFormatter.FormatTurnStartResolved(PlayerId.Player, CurrentBattleState.TurnNumber));
                var impactRequest = BuildImpactAnimationRequest(beforeSnapshot, afterSnapshot, CloneValuePopupEvents(CurrentBattleState));
                if (HasPresentationImpact(impactRequest))
                {
                    yield return PlayImpactAnimationSequence(impactRequest);
                }

                RefreshPresenter();
            }
        }

        private IEnumerator PauseAfterLocalAiAction(IBattleCommand command, float presentationStarted)
        {
            if (CurrentBattleState == null || CurrentBattleState.IsEnded || CurrentBattleState.ActivePlayerId != PlayerId.AI)
            {
                yield break;
            }
            var seconds = AiActionTiming.CalculatePostActionPauseSeconds(
                command is IHandCardCommand, Time.unscaledTime - presentationStarted);
            yield return new WaitForSecondsRealtime((float)seconds);
        }

        private float PlayOnlineAttackAnimation(BattleEventDto attackEvent, IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = BuildOnlineAttackAnimationRequest(attackEvent, battleEvents);
            if (request == null)
            {
                return 0f;
            }

            StartCoroutine(PlayAttackAnimationSequence(request));
            return EstimateAttackAnimationDuration(request);
        }

        private AttackAnimationRequest BuildOnlineAttackAnimationRequest(BattleEventDto attackEvent, IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (attackEvent == null || attackEvent.SourceCoord == null || attackEvent.TargetCoord == null)
            {
                return null;
            }

            var attackerOwnerId = attackEvent.SourceOwnerId;
            var attackerCoord = attackEvent.SourceCoord.ToDomain();
            var defenderOwnerId = attackEvent.TargetOwnerId;
            var declaredTargetCoord = attackEvent.TargetCoord.ToDomain();
            var attacker = CurrentBattleState?.GetBoard(attackerOwnerId)?.GetOccupant(attackerCoord);
            if (attacker == null)
            {
                return null;
            }

            var request = new AttackAnimationRequest(
                attackerOwnerId,
                attackerCoord,
                defenderOwnerId,
                declaredTargetCoord,
                FindOnlineTravelTargetCoord(defenderOwnerId, declaredTargetCoord, battleEvents),
                attacker.AttackType == AttackType.Ranged)
            {
                AttackerCardId = attacker.CardId
            };
            request.DefenderCardId = CurrentBattleState?.GetBoard(defenderOwnerId)?.GetOccupant(request.TravelTargetCoord)?.CardId;

            request.ValuePopupEvents.AddRange(BuildOnlineValuePopupEvents(battleEvents));
            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null || battleEvent.TargetCoord == null)
                {
                    continue;
                }

                var targetOwnerId = battleEvent.TargetOwnerId;
                var targetCoord = battleEvent.TargetCoord.ToDomain();
                var targetsAttacker = IsSameTarget(targetOwnerId, targetCoord, attackerOwnerId, attackerCoord);
                if (targetsAttacker &&
                    battleEvent.ValueCause == BattleValueChangeCause.Counterattack &&
                    (battleEvent.EventType == BattleEventType.DamageApplied ||
                     battleEvent.EventType == BattleEventType.InvinciblePrevented))
                {
                    request.DefenderCounterattacked = true;
                }

                switch (battleEvent.EventType)
                {
                    case BattleEventType.DamageApplied:
                        if (targetsAttacker)
                        {
                            request.AttackerTookDamage = true;
                        }
                        else
                        {
                            AddOrMergeImpact(request.DefenderImpacts, targetOwnerId, targetCoord, tookDamage: true, died: false);
                        }
                        break;

                    case BattleEventType.OccupantRemoved:
                        if (targetsAttacker)
                        {
                            request.AttackerDied = true;
                        }
                        else
                        {
                            AddOrMergeImpact(request.DefenderImpacts, targetOwnerId, targetCoord, tookDamage: false, died: true);
                        }
                        break;
                }
            }

            return request;
        }

        private float PlayOnlineImpactAnimation(
            string sourceCardId,
            BattleEventDto sourceEvent,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = BuildOnlineImpactAnimationRequest(sourceCardId, sourceEvent, battleEvents);
            if (request == null || !HasPresentationImpact(request))
            {
                return 0f;
            }

            var sequence = PlayImpactAnimationSequence(request);
            StartCoroutine(request.SpellEffectId == EndTurnService.RedDragonFlamesEffectId
                ? TrackOnlineRedDragonSequence(sequence, isGroup: false) :
                request.SpellEffectId == MeteorRules.EffectId
                ? TrackOnlineMeteorSequence(sequence, isGroup: false) :
                request.SpellEffectId == FireSpearRules.EffectId ? TrackOnlineFireSpearSequence(sequence, isGroup: false) :
                request.SpellEffectId == PlasmaShieldRules.EffectId ? TrackOnlinePlasmaShieldSequence(sequence, isGroup: false) :
                request.SpellEffectId == ManaShieldRules.EffectId ? TrackOnlineManaShieldSequence(sequence, isGroup: false) :
                request.ReactiveArmourCue != null ? TrackOnlineReactiveArmourSequence(sequence, isGroup: false) :
                request.IronDomeCue != null ? TrackOnlineIronDomeSequence(sequence, isGroup: false) :
                sourceCardId == SummonTrapCardRules.CardId ? TrackOnlineSummonTrapSequence(sequence, isGroup: false) : sequence);
            return EstimateImpactAnimationDuration(request);
        }

        private IEnumerator TrackOnlineRedDragonSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineRedDragonGroups++; else _pendingOnlineRedDragonImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineRedDragonGroups = Mathf.Max(0, _pendingOnlineRedDragonGroups - 1);
                    else _pendingOnlineRedDragonImpacts = Mathf.Max(0, _pendingOnlineRedDragonImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineMeteorSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineMeteorGroups++; else _pendingOnlineMeteorImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineMeteorGroups = Mathf.Max(0, _pendingOnlineMeteorGroups - 1);
                    else _pendingOnlineMeteorImpacts = Mathf.Max(0, _pendingOnlineMeteorImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineFireSpearSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineFireSpearGroups++; else _pendingOnlineFireSpearImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineFireSpearGroups = Mathf.Max(0, _pendingOnlineFireSpearGroups - 1);
                    else _pendingOnlineFireSpearImpacts = Mathf.Max(0, _pendingOnlineFireSpearImpacts - 1);
                }
            }
        }
        private IEnumerator TrackOnlinePlasmaShieldSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlinePlasmaShieldGroups++; else _pendingOnlinePlasmaShieldImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlinePlasmaShieldGroups = Mathf.Max(0, _pendingOnlinePlasmaShieldGroups - 1);
                    else _pendingOnlinePlasmaShieldImpacts = Mathf.Max(0, _pendingOnlinePlasmaShieldImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineManaShieldSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineManaShieldGroups++; else _pendingOnlineManaShieldImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineManaShieldGroups = Mathf.Max(0, _pendingOnlineManaShieldGroups - 1);
                    else _pendingOnlineManaShieldImpacts = Mathf.Max(0, _pendingOnlineManaShieldImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineSummonTrapSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineSummonTrapGroups++; else _pendingOnlineSummonTrapImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineSummonTrapGroups = Mathf.Max(0, _pendingOnlineSummonTrapGroups - 1);
                    else _pendingOnlineSummonTrapImpacts = Mathf.Max(0, _pendingOnlineSummonTrapImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineIronDomeSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineIronDomeGroups++; else _pendingOnlineIronDomeImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineIronDomeGroups = Mathf.Max(0, _pendingOnlineIronDomeGroups - 1);
                    else _pendingOnlineIronDomeImpacts = Mathf.Max(0, _pendingOnlineIronDomeImpacts - 1);
                }
            }
        }

        private IEnumerator TrackOnlineReactiveArmourSequence(IEnumerator sequence, bool isGroup)
        {
            var generation = _combatPresentationGeneration;
            if (isGroup) _pendingOnlineReactiveArmourGroups++; else _pendingOnlineReactiveArmourImpacts++;
            try { yield return sequence; }
            finally
            {
                if (generation == _combatPresentationGeneration)
                {
                    if (isGroup) _pendingOnlineReactiveArmourGroups = Mathf.Max(0, _pendingOnlineReactiveArmourGroups - 1);
                    else _pendingOnlineReactiveArmourImpacts = Mathf.Max(0, _pendingOnlineReactiveArmourImpacts - 1);
                }
            }
        }

        private ImpactAnimationRequest BuildOnlineImpactAnimationRequest(
            string sourceCardId,
            BattleEventDto sourceEvent,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            var request = new ImpactAnimationRequest
            {
                SpellEffectId = sourceEvent?.EventType == BattleEventType.AreaSpellEffectTriggered
                    ? ResolveAreaSpellEffectId(sourceEvent.EffectId)
                    : ResolveDamageSpellEffectId(sourceCardId),
                SpellCasterOwnerId = sourceEvent?.EventType == BattleEventType.SpellCast
                    ? sourceEvent.SourceOwnerId : (PlayerId?)null,
                GuardAreaRuntimeIds = sourceEvent?.EventType == BattleEventType.AreaSpellEffectTriggered ||
                    sourceCardId == EarthquakeRules.CardId || sourceCardId == MeteorRules.CardId || sourceCardId == FireSpearRules.CardId || sourceCardId == TrapMirrorFieldCardRules.CardId || sourceCardId == SummonTrapCardRules.CardId || IsDirectDestructionTrapCard(sourceCardId),
            };
            request.ValuePopupEvents.AddRange(BuildOnlineValuePopupEvents(battleEvents));
            if (sourceCardId == SummonTrapCardRules.CardId && sourceEvent?.TargetCoord != null)
                request.SummonedVisual = CreateOnlineSummonTrapVisual(sourceEvent, battleEvents);
            if (sourceEvent?.EventType == BattleEventType.TrapTriggered &&
                IsDirectDestructionTrapCard(sourceCardId) && sourceEvent.TargetCoord != null)
                ConfigureDestructionTrapCue(request, CaptureBattleSnapshot(CurrentBattleState), sourceCardId,
                    sourceEvent.SourceOwnerId, sourceEvent.TargetOwnerId, sourceEvent.TargetCoord.ToDomain(), sourceEvent.TargetRuntimeId);
            if (sourceEvent?.EventType == BattleEventType.SpellCast && sourceEvent.TargetCoord != null && IsFirewallCard(sourceCardId))
                ConfigureFirewallCast(request, sourceEvent.TargetOwnerId, sourceEvent.TargetCoord.ToDomain());
            if (sourceEvent?.EventType == BattleEventType.SpellCast && sourceEvent.TargetCoord != null && IsFireSpearCard(sourceCardId))
                ConfigureFireSpearCast(request, sourceEvent.TargetOwnerId, sourceEvent.TargetCoord.ToDomain());
            if (sourceEvent?.EventType == BattleEventType.SpellCast && sourceEvent.TargetCoord != null &&
                sourceCardId == PlasmaShieldRules.CardId)
            {
                var coord = sourceEvent.TargetCoord.ToDomain();
                var runtimeId = sourceEvent.TargetRuntimeId;
                if (string.IsNullOrEmpty(runtimeId)) runtimeId = FindTileTextView(sourceEvent.TargetOwnerId, coord)?.VisualRuntimeId;
                ConfigurePlasmaShieldCast(request, sourceEvent.TargetOwnerId, coord, runtimeId);
            }
            if (sourceEvent?.EventType == BattleEventType.SpellCast && sourceEvent.TargetCoord != null &&
                sourceCardId == ManaShieldRules.CardId)
            {
                var coord = sourceEvent.TargetCoord.ToDomain();
                var runtimeId = sourceEvent.TargetRuntimeId;
                if (string.IsNullOrEmpty(runtimeId)) runtimeId = FindTileTextView(sourceEvent.TargetOwnerId, coord)?.VisualRuntimeId;
                ConfigureManaShieldCast(request, sourceEvent.TargetOwnerId, coord, runtimeId);
            }
            if (sourceEvent?.EventType == BattleEventType.TimedBombCountdown)
                request.TimedBombCountdown = new TimedBombCountdownCue(sourceEvent.SourceOwnerId,
                    sourceEvent.EffectIndex, sourceEvent.RemainingTriggersBefore, sourceEvent.RemainingTriggersAfter);

            if (sourceEvent?.EventType == BattleEventType.AreaSpellEffectTriggered)
            {
                var targetCoords = sourceEvent.TargetCoords;
                if ((targetCoords == null || targetCoords.Count == 0) && sourceEvent.TargetCoord != null)
                {
                    targetCoords = new List<TileCoordDto> { sourceEvent.TargetCoord };
                }

                if (targetCoords != null)
                {
                    for (var targetIndex = 0; targetIndex < targetCoords.Count; targetIndex++)
                    {
                        var targetCoord = targetCoords[targetIndex];
                        if (targetCoord != null)
                        {
                            request.SpellEffectTargets.Add(new SpellEffectTarget(
                                sourceEvent.TargetOwnerId,
                                targetCoord.ToDomain()));
                        }
                    }
                }
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null || battleEvent.TargetCoord == null)
                {
                    continue;
                }

                var ownerId = battleEvent.TargetOwnerId;
                var coord = battleEvent.TargetCoord.ToDomain();
                switch (battleEvent.EventType)
                {
                    case BattleEventType.DamageApplied:
                        AddOrMergeImpact(request.Impacts, ownerId, coord, tookDamage: true, died: false);
                        break;

                    case BattleEventType.OccupantRemoved:
                        AddOrMergeImpact(request.Impacts, ownerId, coord, tookDamage: false, died: true);
                        break;
                }
                if (request.GuardAreaRuntimeIds &&
                    (battleEvent.EventType == BattleEventType.DamageApplied || battleEvent.EventType == BattleEventType.OccupantRemoved))
                {
                    var runtimeId = string.IsNullOrWhiteSpace(battleEvent.TargetRuntimeId) ? battleEvent.RuntimeId : battleEvent.TargetRuntimeId;
                    foreach (var impact in request.Impacts)
                        if (impact.OwnerId == ownerId && impact.Coord == coord && !string.IsNullOrWhiteSpace(runtimeId))
                            request.AreaImpactRuntimeIds[impact] = runtimeId;
                }
            }

            if (request.Impacts.Count == 0 &&
                sourceEvent != null &&
                sourceEvent.TargetCoord != null &&
                !string.IsNullOrWhiteSpace(request.SpellEffectId) && request.SpellEffectId != FirewallSpellEffectId && request.SpellEffectId != FireSpearRules.EffectId && request.SpellEffectId != PlasmaShieldRules.EffectId && request.SpellEffectId != ManaShieldRules.EffectId && request.SpellEffectId != EndTurnService.RedDragonFlamesEffectId)
            {
                AddOrMergeImpact(
                    request.Impacts,
                    sourceEvent.TargetOwnerId,
                    sourceEvent.TargetCoord.ToDomain(),
                    tookDamage: true,
                    died: false);
            }

            return request;
        }

        private float PlayOnlineMoveAnimations(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return 0f;
            }

            var duration = 0f;
            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.EventType != BattleEventType.OccupantMoved ||
                    battleEvent.SourceCoord == null ||
                    battleEvent.TargetCoord == null)
                {
                    continue;
                }

                StartCoroutine(PlayOnlineMoveAnimationSequence(
                    battleEvent.SourceOwnerId,
                    battleEvent.SourceCoord.ToDomain(),
                    battleEvent.TargetOwnerId,
                    battleEvent.TargetCoord.ToDomain()));
                duration = Mathf.Max(duration, _attackRunDuration);
            }

            return duration;
        }

        private IEnumerator PlayOnlineMoveAnimationSequence(
            PlayerId sourceOwnerId,
            TileCoord sourceCoord,
            PlayerId targetOwnerId,
            TileCoord targetCoord)
        {
            var sourceView = FindTileTextView(sourceOwnerId, sourceCoord);
            var targetView = FindTileTextView(targetOwnerId, targetCoord);
            if (sourceView == null || targetView == null)
            {
                yield break;
            }

            sourceView.RestoreVisualLayout();
            targetView.RestoreVisualLayout();
            var sourcePosition = sourceView.GetVisualWorldPosition();
            var targetPosition = targetView.GetVisualWorldPosition();
            sourceView.PlayRunAnimation();
            yield return AnimateVisualTravel(sourceView, sourcePosition, targetPosition, _attackRunDuration);
        }

        private static BattleEventDto FindFirstBattleEvent(IReadOnlyList<BattleEventDto> battleEvents, BattleEventType eventType)
        {
            if (battleEvents == null)
            {
                return null;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent != null && battleEvent.EventType == eventType)
                {
                    return battleEvent;
                }
            }

            return null;
        }

        private static bool HasOnlineImpactEvents(IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return false;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null)
                {
                    continue;
                }

                if (battleEvent.EventType == BattleEventType.DamageApplied ||
                    battleEvent.EventType == BattleEventType.HealingApplied ||
                    battleEvent.EventType == BattleEventType.OccupantRemoved)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BattleValuePopupEvent> BuildOnlineValuePopupEvents(IReadOnlyList<BattleEventDto> battleEvents)
        {
            var valuePopupEvents = new List<BattleValuePopupEvent>();
            if (battleEvents == null)
            {
                return valuePopupEvents;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.TargetCoord == null ||
                    (battleEvent.EventType != BattleEventType.DamageApplied &&
                     battleEvent.EventType != BattleEventType.HealingApplied &&
                     battleEvent.EventType != BattleEventType.InvinciblePrevented))
                {
                    continue;
                }

                valuePopupEvents.Add(new BattleValuePopupEvent(
                    battleEvent.RuntimeId,
                    battleEvent.TargetOwnerId,
                    battleEvent.TargetCoord.ToDomain(),
                    battleEvent.EventType == BattleEventType.HealingApplied,
                    battleEvent.Amount,
                    battleEvent.TargetOwnerId,
                    battleEvent.SourceRuntimeId,
                    battleEvent.SourceCardId,
                    battleEvent.ValueCause,
                    battleEvent.DamageType,
                    battleEvent.HpBefore,
                    battleEvent.HpAfter,
                    battleEvent.EventType == BattleEventType.InvinciblePrevented));
            }

            return valuePopupEvents;
        }

        private static void AddOrMergeImpact(
            List<AttackAnimationImpact> impacts,
            PlayerId ownerId,
            TileCoord coord,
            bool tookDamage,
            bool died)
        {
            if (impacts == null)
            {
                return;
            }

            for (var i = 0; i < impacts.Count; i++)
            {
                var existing = impacts[i];
                if (existing == null || !IsSameTarget(existing.OwnerId, existing.Coord, ownerId, coord))
                {
                    continue;
                }

                impacts[i] = new AttackAnimationImpact(
                    ownerId,
                    coord,
                    existing.TookDamage || tookDamage,
                    existing.Died || died);
                return;
            }

            impacts.Add(new AttackAnimationImpact(ownerId, coord, tookDamage, died));
        }

        private static bool IsSameTarget(PlayerId leftOwnerId, TileCoord leftCoord, PlayerId rightOwnerId, TileCoord rightCoord)
        {
            return leftOwnerId == rightOwnerId && leftCoord == rightCoord;
        }

        private static TileCoord FindOnlineTravelTargetCoord(
            PlayerId defenderOwnerId,
            TileCoord declaredTargetCoord,
            IReadOnlyList<BattleEventDto> battleEvents)
        {
            if (battleEvents == null)
            {
                return declaredTargetCoord;
            }

            foreach (var battleEvent in battleEvents)
            {
                if (battleEvent == null ||
                    battleEvent.TargetCoord == null ||
                    battleEvent.EventType != BattleEventType.DamageApplied ||
                    battleEvent.TargetOwnerId != defenderOwnerId)
                {
                    continue;
                }

                return battleEvent.TargetCoord.ToDomain();
            }

            return declaredTargetCoord;
        }

        private float EstimateAttackAnimationDuration(AttackAnimationRequest request)
        {
            if (request == null)
            {
                return 0f;
            }

            var attackerView = FindTileTextView(request.AttackerOwnerId, request.AttackerCoord);
            var isCrossSlash = request.AttackerCardId == GwangmaPresentationAssets.CardId;
            var isFireBreath = request.AttackerCardId == RedDragonPresentationAssets.CardId;
            var isFireCounter = request.DefenderCounterattacked && request.DefenderCardId == RedDragonPresentationAssets.CardId;
            var travelDuration = (request.AttackerCardId == A301PresentationAssets.CardId || request.AttackerCardId == ManaCannonPresentationAssets.CardId) ? 0f : request.IsRangedAttacker
                ? _rangedAttackNudgeDuration * 2f
                : GetMeleeApproachDuration(request.AttackerCardId) + GetMeleeReturnDuration(request.AttackerCardId);
            var attackDuration = attackerView == null
                ? _attackImpactDelay
                : attackerView.GetAttackAnimationDurationSeconds();
            var popupDuration = request.ValuePopupEvents.Count > 0
                ? (request.ValuePopupEvents.Count * Mathf.Max(0f, _valuePopupCascadeDelay)) + Mathf.Max(0f, _valuePopupCascadeDelay)
                : 0f;
            var deathDuration = (request.AttackerDied || HasAnyDeathImpact(request.DefenderImpacts))
                ? Mathf.Max(0f, _deathHoldDuration)
                : 0f;

            var reactionDuration = Mathf.Max(0f, _attackReactionDuration);
            foreach (var impact in request.DefenderImpacts)
            {
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null) continue;
                if (impact.TookDamage) reactionDuration = Mathf.Max(reactionDuration, view.GetBeAttackedAnimationDurationSeconds());
                if (impact.Died) deathDuration = Mathf.Max(deathDuration, view.GetDeathAnimationDurationSeconds());
            }
            if (attackerView != null)
            {
                if (request.AttackerDied) deathDuration = Mathf.Max(deathDuration, attackerView.GetDeathAnimationDurationSeconds());
            }
            if (request.AttackerCardId == BlueDragonPresentationAssets.CardId)
            {
                attackDuration = BlueDragonPresentationAssets.WindupDuration + BlueDragonPresentationAssets.FlightDuration;
                reactionDuration = Mathf.Max(reactionDuration, BlueDragonPresentationAssets.ImpactDuration);
            }
            if (request.AttackerCardId == A301PresentationAssets.CardId)
                attackDuration = Mathf.Max(attackDuration, A301PresentationAssets.WindupDuration + A301PresentationAssets.ImpactDuration);
            if (request.AttackerCardId == ManaCannonPresentationAssets.CardId)
                attackDuration = Mathf.Max(attackDuration, ManaCannonPresentationAssets.TotalDuration);
            if (isFireBreath) attackDuration = Mathf.Max(attackDuration, RedDragonPresentationAssets.WindupDuration + RedDragonPresentationAssets.BreathDuration);
            if (isCrossSlash || isFireBreath || isFireCounter || (request.DefenderCounterattacked && request.DefenderCardId == GwangmaPresentationAssets.CardId))
            {
                var impactDelay = isCrossSlash ? GwangmaPresentationAssets.WindupDuration :
                    isFireBreath ? RedDragonPresentationAssets.WindupDuration :
                    attackDuration > 0f ? Mathf.Min(_attackImpactDelay, attackDuration) : _attackImpactDelay;
                if (request.DefenderCounterattacked)
                {
                    var defenderView = FindTileTextView(request.DefenderOwnerId, request.TravelTargetCoord);
                    if (defenderView != null) attackDuration = Mathf.Max(attackDuration,
                        impactDelay + Mathf.Max(defenderView.GetAttackAnimationDurationSeconds(),
                            isFireCounter ? RedDragonPresentationAssets.WindupDuration + RedDragonPresentationAssets.BreathDuration : 0f));
                }
                reactionDuration = Mathf.Max(reactionDuration, GwangmaPresentationAssets.ImpactDuration);
            }
            if (request.AttackerCardId == GaebangChiefPresentationAssets.CardId ||
                (request.DefenderCounterattacked && request.DefenderCardId == GaebangChiefPresentationAssets.CardId) ||
                request.AttackerCardId == A212PresentationAssets.CardId ||
                (request.DefenderCounterattacked && request.DefenderCardId == A212PresentationAssets.CardId))
            {
                var defenderView = FindTileTextView(request.DefenderOwnerId, request.TravelTargetCoord);
                var attackerHits = attackerView == null ? 1 : attackerView.PresentedHitsPerAttack;
                attackDuration = Mathf.Max(attackDuration, A212PresentationAssets.WindupDuration +
                    GetContactEffectDuration(request.AttackerCardId, attackerHits));
                if (request.DefenderCounterattacked && defenderView != null)
                    attackDuration = Mathf.Max(attackDuration, Mathf.Max(defenderView.GetAttackAnimationDurationSeconds(),
                        A212PresentationAssets.WindupDuration + GetContactEffectDuration(request.DefenderCardId, defenderView.PresentedHitsPerAttack)));
            }
            return travelDuration + attackDuration + popupDuration + reactionDuration + deathDuration + 0.1f;
        }

        private float EstimateImpactAnimationDuration(ImpactAnimationRequest request)
        {
            if (request == null)
            {
                return 0f;
            }

            var spellDuration = GetSpellEffectDuration(request.SpellEffectId);
            if (request.ReactiveArmourCue != null) spellDuration += ReactiveArmourVfx.DurationSeconds;
            if (request.IronDomeCue != null) spellDuration += IronDomeMissileVfx.DurationSeconds;
            if (request.TimedBombCountdown != null)
                spellDuration += TimedBombPresentationVfx.DurationFor(request.TimedBombCountdown.Before, request.TimedBombCountdown.After);
            foreach (var step in request.TimedBombSteps) spellDuration += EstimateImpactAnimationDuration(step);
            foreach (var step in request.AreaSpellSteps) spellDuration += EstimateImpactAnimationDuration(step);
            var popupDuration = request.ValuePopupEvents.Count > 0
                ? (request.ValuePopupEvents.Count * Mathf.Max(0f, _valuePopupCascadeDelay)) + Mathf.Max(0f, _valuePopupCascadeDelay)
                : 0f;
            var deathDuration = HasAnyDeathImpact(request.Impacts)
                ? Mathf.Max(0f, _deathHoldDuration)
                : 0f;
            var reactionDuration = HasAnyDamageImpact(request.Impacts)
                ? Mathf.Max(0f, _attackReactionDuration)
                : 0f;
            foreach (var impact in request.Impacts)
            {
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null) continue;
                if (impact.TookDamage) reactionDuration = Mathf.Max(reactionDuration, view.GetBeAttackedAnimationDurationSeconds());
                if (impact.Died) deathDuration = Mathf.Max(deathDuration, view.GetDeathAnimationDurationSeconds());
            }
            var completionPadding = spellDuration > 0f || popupDuration > 0f ||
                                    reactionDuration > 0f || deathDuration > 0f
                ? 0.1f
                : 0f;

            return spellDuration + popupDuration + reactionDuration + deathDuration + completionPadding;
        }

        private static bool HasAnyDeathImpact(IReadOnlyList<AttackAnimationImpact> impacts)
        {
            if (impacts == null)
            {
                return false;
            }

            foreach (var impact in impacts)
            {
                if (impact != null && impact.Died)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyDamageImpact(IReadOnlyList<AttackAnimationImpact> impacts)
        {
            if (impacts == null)
            {
                return false;
            }

            foreach (var impact in impacts)
            {
                if (impact != null && impact.TookDamage)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator PlayAttackAnimationSequence(AttackAnimationRequest request)
        {
            if (request == null)
            {
                yield break;
            }

            var presentationGeneration = _combatPresentationGeneration;
            var isWaterCannon = request.AttackerCardId == BlueDragonPresentationAssets.CardId;
            var isLaser = request.AttackerCardId == A301PresentationAssets.CardId;
            var isManaCannon = request.AttackerCardId == ManaCannonPresentationAssets.CardId;
            var isCrossSlash = request.AttackerCardId == GwangmaPresentationAssets.CardId;
            var isFireBreath = request.AttackerCardId == RedDragonPresentationAssets.CardId;
            var isFireCounter = request.DefenderCounterattacked && request.DefenderCardId == RedDragonPresentationAssets.CardId;
            var attackerView = FindTileTextView(request.AttackerOwnerId, request.AttackerCoord);
            if (attackerView == null)
            {
                yield break;
            }

            var destinationView = FindTileTextView(request.DefenderOwnerId, request.TravelTargetCoord) ??
                                  FindTileTextView(request.DefenderOwnerId, request.DeclaredTargetCoord);

            var attackerRuntimeId = attackerView.VisualRuntimeId;
            var targetRuntimeId = destinationView == null ? null : destinationView.VisualRuntimeId;
            var laserImpactIdentities = (isLaser || isManaCannon) ? CaptureLaserImpactIdentities(request) : null;
            attackerView.RestoreVisualLayout();
            destinationView?.RestoreVisualLayout();
            var attackerStartPosition = attackerView.GetVisualWorldPosition();
            var attackDestination = destinationView != null
                ? destinationView.GetVisualWorldPosition()
                : attackerStartPosition;
            var attackAnimationDuration = attackerView.GetAttackAnimationDurationSeconds();
            var shouldTravelForAttack = !request.IsRangedAttacker;
            var meleeContactTarget = shouldTravelForAttack
                ? GetMeleeAttackContactTarget(destinationView, request.DefenderOwnerId, attackDestination, _meleeAttackContactOffset, _meleeAttackSideOffset)
                : attackerStartPosition;
            if (isCrossSlash && shouldTravelForAttack)
            {
                // The legacy contact offset is authored for full-size sprites. Keep the
                // compact atlas grounded at that same contact point instead of rising.
                var visualHeight = attackerView.GetVisualNormalizedWorldPosition(new Vector2(0.5f, 1f)) -
                                   attackerView.GetVisualNormalizedWorldPosition(new Vector2(0.5f, 0f));
                meleeContactTarget -= visualHeight * (1f / GwangmaPresentationAssets.BoardScale - 1f) * 0.5f;
            }
            if ((isFireBreath || isFireCounter) && shouldTravelForAttack)
                meleeContactTarget = RedDragonFireBreath.GetMeleeContactPosition(attackerView, destinationView, meleeContactTarget);
            var rangedNudgeTarget = shouldTravelForAttack
                ? attackerStartPosition
                : GetRangedAttackNudgeTarget(
                    attackerStartPosition,
                    attackDestination,
                    _rangedAttackNudgeDistance * attackerView.GetResponsiveTileScale());

            if (shouldTravelForAttack)
            {
                attackerView.PlayRunAnimation();
                if (isCrossSlash) GetGwangmaCrossSlash().PlayDash();
                yield return AnimateVisualTravel(attackerView, attackerStartPosition, meleeContactTarget,
                    GetMeleeApproachDuration(request.AttackerCardId));
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
            else
            {
                attackerView.SetVisualWorldPosition(attackerStartPosition);
                if (!isLaser && !isManaCannon) yield return AnimateVisualTravel(attackerView, attackerStartPosition, rangedNudgeTarget, _rangedAttackNudgeDuration);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }

            if (request.AttackerCardId == GaebangChiefPresentationAssets.CardId ||
                (request.DefenderCounterattacked && request.DefenderCardId == GaebangChiefPresentationAssets.CardId) ||
                request.AttackerCardId == A212PresentationAssets.CardId ||
                (request.DefenderCounterattacked && request.DefenderCardId == A212PresentationAssets.CardId))
            {
                yield return PlayContactAttackExchange(request, attackerView, destinationView, presentationGeneration,
                    attackerRuntimeId, targetRuntimeId);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
            else if (isManaCannon)
            {
                yield return PlayManaCannonAttackExchange(request, attackerView, destinationView, presentationGeneration, attackerRuntimeId, targetRuntimeId, laserImpactIdentities);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
            else if (isLaser)
            {
                yield return PlayLaserAttackExchange(request, attackerView, destinationView, presentationGeneration, attackerRuntimeId, targetRuntimeId, laserImpactIdentities);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
            else
            {
                attackerView.PlayAttackAnimation();
                if (isWaterCannon) GetBlueDragonWaterCannon().PlayCharge();
                if (isCrossSlash && destinationView != null) GetGwangmaCrossSlash().PlayStrike(attackerView, destinationView);
                if (isFireBreath && destinationView != null) GetRedDragonFireBreath().PlayStrike(attackerView, destinationView);
                if (isFireBreath) attackAnimationDuration = Mathf.Max(attackAnimationDuration, RedDragonPresentationAssets.WindupDuration + RedDragonPresentationAssets.BreathDuration);
                var attackImpactDelay = isWaterCannon
                    ? BlueDragonPresentationAssets.WindupDuration
                    : isCrossSlash ? GwangmaPresentationAssets.WindupDuration
                    : isFireBreath ? RedDragonPresentationAssets.WindupDuration
                    : attackAnimationDuration > 0f
                    ? Mathf.Min(_attackImpactDelay, attackAnimationDuration)
                    : _attackImpactDelay;
                if (attackImpactDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(attackImpactDelay);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

                if (isWaterCannon && destinationView != null)
                {
                    yield return GetBlueDragonWaterCannon().PlayFlight(attackerView, destinationView);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

                var defenderCounterattacked = request.DefenderCounterattacked && destinationView != null;
                var defenderCounterattackDuration = 0f;
                if (defenderCounterattacked)
                {
                    destinationView.PlayAttackAnimation();
                    if (request.DefenderCardId == GwangmaPresentationAssets.CardId)
                        GetGwangmaCrossSlash().PlayStrike(destinationView, attackerView, counterattack: true);
                    defenderCounterattackDuration = destinationView.GetAttackAnimationDurationSeconds();
                    if (isFireCounter)
                    {
                        GetRedDragonFireBreath().PlayStrike(destinationView, attackerView, counterattack: true);
                        defenderCounterattackDuration = Mathf.Max(defenderCounterattackDuration, RedDragonPresentationAssets.WindupDuration + RedDragonPresentationAssets.BreathDuration);
                    }
                }

                var remainingAttackDuration = Mathf.Max(0f, attackAnimationDuration - attackImpactDelay -
                    (isWaterCannon ? BlueDragonPresentationAssets.FlightDuration : 0f));
                var attackPhaseWaitDuration = Mathf.Max(remainingAttackDuration, defenderCounterattackDuration);
                if (attackPhaseWaitDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(attackPhaseWaitDuration);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

                var valuePopupCascadeDuration = QueueValuePopupAnimations(request.ValuePopupEvents);
                if (!isWaterCannon && !isFireBreath && !isFireCounter && valuePopupCascadeDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(valuePopupCascadeDuration);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

                var reactionDuration = 0f;
                foreach (var impact in request.DefenderImpacts)
                {
                    if (!impact.TookDamage)
                    {
                        continue;
                    }

                    var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                    impactView?.PlayBeAttackedAnimation();

                    if (impactView != null)
                    {
                        reactionDuration = Mathf.Max(reactionDuration, impactView.GetBeAttackedAnimationDurationSeconds());
                    }
                }

                if (request.AttackerTookDamage)
                {
                    // The initiating attacker keeps its attack pose through counterattack damage.
                    attackerView.PlayDamageSound();
                }

                var reactionWaitDuration = Mathf.Max(_attackReactionDuration, reactionDuration);
                if (isCrossSlash || (defenderCounterattacked && request.DefenderCardId == GwangmaPresentationAssets.CardId))
                    reactionWaitDuration = Mathf.Max(reactionWaitDuration, GwangmaPresentationAssets.ImpactDuration);
                if (isWaterCannon) reactionWaitDuration = Mathf.Max(reactionWaitDuration,
                    Mathf.Max(valuePopupCascadeDuration, BlueDragonPresentationAssets.ImpactDuration));
                if (isFireBreath || isFireCounter) reactionWaitDuration = Mathf.Max(reactionWaitDuration, valuePopupCascadeDuration);
                if (reactionWaitDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(reactionWaitDuration);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

            }

            if ((isLaser || isManaCannon) && !AreLaserImpactIdentitiesCurrent(laserImpactIdentities))
            {
                if (_a301LaserAttack != null) _a301LaserAttack.Cancel();
            if (_gaebangChiefCoinPunch != null) _gaebangChiefCoinPunch.Cancel();
            if (_manaCannonProjectile != null) _manaCannonProjectile.Cancel();
                attackerView.ResetCombatPresentation();
                foreach (var entry in laserImpactIdentities)
                    if (entry.Key != null && entry.Key.isActiveAndEnabled && entry.Key.VisualRuntimeId == entry.Value)
                        entry.Key.ResetCombatPresentation();
                yield break;
            }
            var hasAnyDeath = false;
            var deathDuration = 0f;
            var counterattackerFinalized = false;
            foreach (var impact in request.DefenderImpacts)
            {
                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (impactView == null)
                {
                    continue;
                }

                if (impactView == destinationView && (impact.Died || impact.TookDamage))
                    counterattackerFinalized = true;

                if (impact.Died)
                {
                    impactView.PlayDeathAnimation();
                    hasAnyDeath = true;
                    deathDuration = Mathf.Max(deathDuration, impactView.GetDeathAnimationDurationSeconds());
                }
                else if (impact.TookDamage)
                {
                    impactView.PlayIdleAnimation();
                }
            }

            // A blocked/invincible hit can cause a counterattack without a defender
            // damage impact. Restore that survivor before the attacker's lethal exit.
            if (request.DefenderCounterattacked && !counterattackerFinalized &&
                destinationView != null && destinationView.isActiveAndEnabled &&
                destinationView.VisualRuntimeId == targetRuntimeId)
                destinationView.PlayIdleAnimation();

            if (request.AttackerDied)
            {
                attackerView.PlayDeathAnimation();
                hasAnyDeath = true;
                deathDuration = Mathf.Max(deathDuration, attackerView.GetDeathAnimationDurationSeconds());

                var deathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
                if (hasAnyDeath && deathWaitDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(deathWaitDuration);
                    if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
                }

                yield break;
            }

            if (shouldTravelForAttack)
            {
                attackerView.PlayRunAnimation();
                yield return AnimateVisualTravel(attackerView, meleeContactTarget, attackerStartPosition,
                    GetMeleeReturnDuration(request.AttackerCardId));
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
            else
            {
                if (!isLaser && !isManaCannon) yield return AnimateVisualTravel(attackerView, rangedNudgeTarget, attackerStartPosition, _rangedAttackNudgeDuration);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }

            attackerView.RestoreVisualLayout();
            attackerView.PlayIdleAnimation();

            var finalDeathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
            if (hasAnyDeath && finalDeathWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(finalDeathWaitDuration);
                if (!IsAttackPresentationCurrent(presentationGeneration, attackerView, attackerRuntimeId, destinationView, targetRuntimeId)) yield break;
            }
        }

        private Dictionary<TileTextView, string> CaptureLaserImpactIdentities(AttackAnimationRequest request)
        {
            var identities = new Dictionary<TileTextView, string>();
            foreach (var impact in request.DefenderImpacts)
            {
                if (!impact.TookDamage && !impact.Died) continue;
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view != null) identities[view] = view.VisualRuntimeId;
            }
            return identities;
        }

        private static bool AreLaserImpactIdentitiesCurrent(Dictionary<TileTextView, string> identities)
        {
            foreach (var entry in identities)
                if (entry.Key == null || !entry.Key.isActiveAndEnabled || entry.Key.VisualRuntimeId != entry.Value) return false;
            return true;
        }


        private IEnumerator PlayManaCannonAttackExchange(AttackAnimationRequest request, TileTextView attacker,
            TileTextView target, int generation, string attackerId, string targetId,
            Dictionary<TileTextView, string> impactIdentities)
        {
            var effect = GetManaCannonProjectile();
            // Warm native resources before starting either animation clock.
            var frames = ManaCannonPresentationAssets.ProjectileFrames;
            var impactFrames = ManaCannonPresentationAssets.ImpactFrames;
            var shotSound = ManaCannonPresentationAssets.AttackSound;
            var impactSound = ManaCannonPresentationAssets.ImpactSound;
            attacker.PlayAttackAnimation();
            if (target != null) effect.PlayShot(attacker, target);
            var startedAt = Time.unscaledTime;
            var completed = false;
            try
            {
                while (Time.unscaledTime - startedAt < ManaCannonPresentationAssets.ContactAt)
                {
                    if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId) ||
                        !AreLaserImpactIdentitiesCurrent(impactIdentities)) yield break;
                    yield return null;
                }
                if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId) ||
                    !AreLaserImpactIdentitiesCurrent(impactIdentities)) yield break;
                // Only display gateway-recorded packets when the projectile reaches its target.
                var perTargetSteps = new Dictionary<string, int>(StringComparer.Ordinal);
                var soundedTargets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cue in request.ValuePopupEvents)
                {
                    if (cue == null || (cue.Amount <= 0 && !cue.IsInvinciblePrevented)) continue;
                    var view = FindTileTextView(cue.OwnerId, cue.Coord);
                    if (view == null || !view.isActiveAndEnabled ||
                        (!string.IsNullOrWhiteSpace(cue.RuntimeId) && view.VisualRuntimeId != cue.RuntimeId)) continue;
                    var key = string.IsNullOrWhiteSpace(cue.RuntimeId) ? $"{cue.OwnerId}:{cue.Coord}" : cue.RuntimeId;
                    perTargetSteps.TryGetValue(key, out var step);
                    perTargetSteps[key] = step + 1;
                    if (cue.IsInvinciblePrevented) view.QueueInvinciblePopup(0f, step);
                    else
                    {
                        view.QueueFloatingValuePopup(cue.Amount, cue.IsHealing, 0f, step);
                        if (!cue.IsHealing) { view.PlayDamageSound(); soundedTargets.Add(view.VisualRuntimeId); }
                    }
                }
                var reactionDuration = Mathf.Max(0f, _attackReactionDuration);
                foreach (var impact in request.DefenderImpacts)
                {
                    if (!impact.TookDamage) continue;
                    var view = FindTileTextView(impact.OwnerId, impact.Coord);
                    if (view == null || !view.isActiveAndEnabled) continue;
                    view.PlayDamageReactionAnimation();
                    if (!soundedTargets.Contains(view.VisualRuntimeId)) view.PlayDamageSound();
                    reactionDuration = Mathf.Max(reactionDuration, view.GetBeAttackedAnimationDurationSeconds());
                }
                var finishAt = Mathf.Max(attacker.GetAttackAnimationDurationSeconds(),
                    Mathf.Max(ManaCannonPresentationAssets.TotalDuration,
                        Time.unscaledTime - startedAt + Mathf.Max(reactionDuration, impactSound == null ? 0f : impactSound.length)));
                while (Time.unscaledTime - startedAt < finishAt)
                {
                    if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId) ||
                        !AreLaserImpactIdentitiesCurrent(impactIdentities)) yield break;
                    yield return null;
                }
                completed = true;
            }
            finally
            {
                if (!completed && generation == _combatPresentationGeneration)
                {
                    effect.Cancel();
                    if (attacker != null && attacker.isActiveAndEnabled && attacker.VisualRuntimeId == attackerId)
                        attacker.ResetCombatPresentation();
                    foreach (var entry in impactIdentities)
                        if (entry.Key != null && entry.Key.isActiveAndEnabled && entry.Key.VisualRuntimeId == entry.Value)
                            entry.Key.ResetCombatPresentation();
                }
            }
        }

        private IEnumerator PlayLaserAttackExchange(AttackAnimationRequest request, TileTextView attacker,
            TileTextView target, int generation, string attackerId, string targetId,
            Dictionary<TileTextView, string> impactIdentities)
        {
            attacker.PlayAttackAnimation();
            var attackDuration = Mathf.Max(attacker.GetAttackAnimationDurationSeconds(),
                A301PresentationAssets.WindupDuration + A301PresentationAssets.BeamDuration);
            yield return new WaitForSecondsRealtime(A301PresentationAssets.WindupDuration);
            if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId) ||
                !AreLaserImpactIdentitiesCurrent(impactIdentities)) yield break;

            if (target != null) GetA301LaserAttack().PlayShot(attacker, target);
            // Present recorded packets at beam emission; never apply damage or resolve counters here.
            var perTargetSteps = new Dictionary<string, int>(StringComparer.Ordinal);
            var soundedTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in request.ValuePopupEvents)
            {
                if (cue == null || (cue.Amount <= 0 && !cue.IsInvinciblePrevented)) continue;
                var view = FindTileTextView(cue.OwnerId, cue.Coord);
                if (view == null || !view.isActiveAndEnabled ||
                    (!string.IsNullOrWhiteSpace(cue.RuntimeId) && view.VisualRuntimeId != cue.RuntimeId)) continue;
                var key = string.IsNullOrWhiteSpace(cue.RuntimeId) ? $"{cue.OwnerId}:{cue.Coord}" : cue.RuntimeId;
                perTargetSteps.TryGetValue(key, out var step);
                perTargetSteps[key] = step + 1;
                if (cue.IsInvinciblePrevented) view.QueueInvinciblePopup(0f, step);
                else
                {
                    view.QueueFloatingValuePopup(cue.Amount, cue.IsHealing, 0f, step);
                    if (!cue.IsHealing) { view.PlayDamageSound(); soundedTargets.Add(view.VisualRuntimeId); }
                }
            }
            var reactionDuration = Mathf.Max(0f, _attackReactionDuration);
            foreach (var impact in request.DefenderImpacts)
            {
                if (!impact.TookDamage) continue;
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null || !view.isActiveAndEnabled) continue;
                view.PlayDamageReactionAnimation();
                if (!soundedTargets.Contains(view.VisualRuntimeId)) view.PlayDamageSound();
                reactionDuration = Mathf.Max(reactionDuration, view.GetBeAttackedAnimationDurationSeconds());
            }
            var sound = A301PresentationAssets.AttackSound;
            var remaining = Mathf.Max(attackDuration - A301PresentationAssets.WindupDuration,
                Mathf.Max(reactionDuration, Mathf.Max(A301PresentationAssets.ImpactDuration, sound == null ? 0f : sound.length)));
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }


        private static float GetContactEffectDuration(string cardId, int hits)
        {
            return cardId == GaebangChiefPresentationAssets.CardId
                ? GaebangChiefPresentationAssets.ImpactDuration
                : A212PresentationAssets.GetGrindDuration(hits, A212PresentationAssets.HitInterval);
        }

        private IEnumerator PlayContactAttackExchange(AttackAnimationRequest request, TileTextView attacker,
            TileTextView target, int generation, string attackerId, string targetId)
        {
            var counterattacked = request.DefenderCounterattacked && target != null;
            var hitInterval = A212PresentationAssets.HitInterval;
            var windup = A212PresentationAssets.WindupDuration;
            var attackerHits = attacker.PresentedHitsPerAttack;
            var counterHits = counterattacked ? target.PresentedHitsPerAttack : 0;
            attacker.PlayAttackAnimation();
            StartContactExchangeEffect(attacker, target, request.AttackerCardId, attackerHits, false);
            if (counterattacked)
            {
                target.PlayAttackAnimation();
                StartContactExchangeEffect(target, attacker, request.DefenderCardId, counterHits, true);
            }

            var duration = Mathf.Max(attacker.GetAttackAnimationDurationSeconds(),
                windup + GetContactEffectDuration(request.AttackerCardId, attackerHits));
            if (counterattacked) duration = Mathf.Max(duration,
                Mathf.Max(target.GetAttackAnimationDurationSeconds(),
                    windup + GetContactEffectDuration(request.DefenderCardId, counterHits)));
            // Keep each existing target's popup order, including prevention and LifeSteal.
            // Emit when due instead of queuing delayed work that could survive a cancelled exchange.
            var cueTimes = new float[request.ValuePopupEvents.Count];
            var cueShown = new bool[cueTimes.Length];
            var cueSteps = new int[cueTimes.Length];
            var perTargetSteps = new Dictionary<string, int>(StringComparer.Ordinal);
            var hasDamageCue = false;
            for (var i = 0; i < cueTimes.Length; i++)
            {
                var cue = request.ValuePopupEvents[i];
                if (cue == null || (cue.Amount <= 0 && !cue.IsInvinciblePrevented)) continue;
                var key = string.IsNullOrWhiteSpace(cue.RuntimeId) ? $"{cue.OwnerId}:{cue.Coord}" : cue.RuntimeId;
                perTargetSteps.TryGetValue(key, out var step);
                cueTimes[i] = windup + step * hitInterval;
                cueSteps[i] = step;
                perTargetSteps[key] = step + 1;
                duration = Mathf.Max(duration, cueTimes[i] + hitInterval);
                hasDamageCue |= !cue.IsHealing && !cue.IsInvinciblePrevented && cue.Amount > 0;
            }

            var startedAt = Time.unscaledTime;
            var fallbackHitPlayed = false;
            while (Time.unscaledTime - startedAt < duration)
            {
                if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId)) yield break;
                var elapsed = Time.unscaledTime - startedAt;
                for (var i = 0; i < cueTimes.Length; i++)
                {
                    var cue = request.ValuePopupEvents[i];
                    if (cueShown[i] || cue == null || (cue.Amount <= 0 && !cue.IsInvinciblePrevented) ||
                        elapsed < cueTimes[i]) continue;
                    cueShown[i] = true;
                    var view = FindTileTextView(cue.OwnerId, cue.Coord);
                    if (view == null || !view.isActiveAndEnabled ||
                        (!string.IsNullOrWhiteSpace(cue.RuntimeId) && view.VisualRuntimeId != cue.RuntimeId)) continue;
                    if (cue.IsInvinciblePrevented) view.QueueInvinciblePopup(0f, cueSteps[i]);
                    else
                    {
                        view.QueueFloatingValuePopup(cue.Amount, cue.IsHealing, 0f, cueSteps[i]);
                        if (!cue.IsHealing) view.PlayDamageSound();
                    }
                }
                if (!fallbackHitPlayed && !hasDamageCue && elapsed >= windup)
                {
                    // Compatibility for summary-only presentation requests; never invent damage.
                    foreach (var impact in request.DefenderImpacts)
                        if (impact.TookDamage) FindTileTextView(impact.OwnerId, impact.Coord)?.PlayDamageSound();
                    if (request.AttackerTookDamage) attacker.PlayDamageSound();
                    fallbackHitPlayed = true;
                }
                yield return null;
            }

            if (!IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId)) yield break;

            var reactionDuration = Mathf.Max(0f, _attackReactionDuration);
            foreach (var impact in request.DefenderImpacts)
            {
                if (!impact.TookDamage) continue;
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null) continue;
                view.PlayDamageReactionAnimation();
                reactionDuration = Mathf.Max(reactionDuration, view.GetBeAttackedAnimationDurationSeconds());
            }
            // The initiating attacker keeps its Attack pose through counterattack damage.
            if (reactionDuration > 0f) yield return new WaitForSecondsRealtime(reactionDuration);
            if (counterattacked && IsAttackPresentationCurrent(generation, attacker, attackerId, target, targetId))
                target.PlayIdleAnimation();
        }

        private void StartContactExchangeEffect(TileTextView attacker, TileTextView target, string cardId,
            int hits, bool counterattack)
        {
            if (attacker == null || target == null) return;
            if (cardId == A212PresentationAssets.CardId)
                GetA212SawAttack().PlayStrike(attacker, target, hits, A212PresentationAssets.HitInterval,
                    A212PresentationAssets.WindupDuration, counterattack);
            else if (cardId == GaebangChiefPresentationAssets.CardId)
                GetGaebangChiefCoinPunch().PlayStrike(attacker, target, hits, A212PresentationAssets.HitInterval,
                    GaebangChiefPresentationAssets.WindupDuration, counterattack);
            else if (cardId == GwangmaPresentationAssets.CardId)
                GetGwangmaCrossSlash().PlayStrike(attacker, target, counterattack);
            else if (cardId == RedDragonPresentationAssets.CardId)
                GetRedDragonFireBreath().PlayStrike(attacker, target, counterattack);
        }


        private bool IsAttackPresentationCurrent(int generation, TileTextView attacker, string attackerId,
            TileTextView target, string targetId)
        {
            return generation == _combatPresentationGeneration && attacker != null &&
                attacker.isActiveAndEnabled && attacker.VisualRuntimeId == attackerId &&
                (targetId == null || (target != null && target.isActiveAndEnabled && target.VisualRuntimeId == targetId));
        }

        private IEnumerator PlayImpactAnimationSequence(ImpactAnimationRequest request)
        {
            if (request == null || !HasPresentationImpact(request))
            {
                yield break;
            }

            var presentationGeneration = _combatPresentationGeneration;
            if (request.SummonedVisual != null && !PresentSummonTrapVisual(request.SummonedVisual)) yield break;
            foreach (var step in request.TimedBombSteps)
            {
                if (presentationGeneration != _combatPresentationGeneration) yield break;
                yield return PlayImpactAnimationSequence(step);
            }
            foreach (var step in request.AreaSpellSteps)
            {
                if (presentationGeneration != _combatPresentationGeneration) yield break;
                yield return PlayImpactAnimationSequence(step);
            }
            if (presentationGeneration != _combatPresentationGeneration) yield break;
            if (request.TimedBombCountdown != null)
            {
                var countdownDuration = PlayTimedBombCountdown(request.TimedBombCountdown);
                if (countdownDuration > 0) yield return new WaitForSecondsRealtime(countdownDuration);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
            if (request.IronDomeCue != null)
            {
                var duration = PlayIronDome(request);
                if (duration > 0f) yield return new WaitForSecondsRealtime(IronDomeMissileVfx.ImpactDelaySeconds);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
                // A replacement occupant or a disabled source cancels the missile. Do not
                // start the confirmed Death clip on a different body at that coordinate.
                var impactReady = duration <= 0f || (_ironDomeMissileVfx != null && _ironDomeMissileVfx.TryBeginExplosion());
                if (impactReady) PlayIronDomeAttackerDeath(request);
            }
            if (request.ReactiveArmourCue != null)
            {
                var duration = PlayReactiveArmour(request);
                if (duration > 0f) yield return new WaitForSecondsRealtime(ReactiveArmourVfx.EngulfDelaySeconds);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
                if (duration > 0f) _reactiveArmourVfx?.TryBeginEngulf();
                PlayReactiveArmourAttackerDeath(request);
            }
            ProjectDeferredSpellHp(request, afterImpact: false);
            yield return PlaySpellEffectAnimationSequence(request);
            if (presentationGeneration != _combatPresentationGeneration) yield break;
            ProjectDeferredSpellHp(request, afterImpact: true);

            var popups = request.GuardAreaRuntimeIds ? request.ValuePopupEvents.FindAll(popup =>
            {
                var view = FindTileTextView(popup.OwnerId, popup.Coord);
                return view != null && view.isActiveAndEnabled && view.VisualRuntimeId == popup.RuntimeId;
            }) : request.ValuePopupEvents;
            var valuePopupCascadeDuration = QueueValuePopupAnimations(popups);
            // Meteor popups and Hurt begin together at impact while the explosion continues.
            if (valuePopupCascadeDuration > 0f && request.SpellEffectId != MeteorRules.EffectId && request.SpellEffectId != FireSpearRules.EffectId && request.SpellEffectId != EndTurnService.RedDragonFlamesEffectId)
            {
                yield return new WaitForSecondsRealtime(valuePopupCascadeDuration);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }

            var reactionDuration = 0f;
            foreach (var impact in request.Impacts)
            {
                if (!impact.TookDamage)
                {
                    continue;
                }

                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (!MatchesAreaImpactIdentity(request, impact, impactView)) continue;
                impactView?.PlayBeAttackedAnimation();
                if (impactView != null)
                {
                    reactionDuration = Mathf.Max(reactionDuration, impactView.GetBeAttackedAnimationDurationSeconds());
                }
            }

            var reactionWaitDuration = reactionDuration > 0f
                ? Mathf.Max(_attackReactionDuration, reactionDuration)
                : 0f;
            if (reactionWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(reactionWaitDuration);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }

            var hasAnyDeath = request.ReactiveArmourCue?.DeathStarted == true || request.IronDomeCue?.DeathStarted == true;
            var deathDuration = Mathf.Max(
                request.ReactiveArmourCue?.DeathStarted == true ? Mathf.Max(0f, request.ReactiveArmourCue.DeathEndsAt - Time.unscaledTime) : 0f,
                request.IronDomeCue?.DeathStarted == true ? Mathf.Max(0f, request.IronDomeCue.DeathEndsAt - Time.unscaledTime) : 0f);
            foreach (var impact in request.Impacts)
            {
                var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                if (impactView == null || !MatchesAreaImpactIdentity(request, impact, impactView))
                {
                    continue;
                }

                if (impact.Died)
                {
                    if ((IsReactiveArmourAttackerDeath(request, impact) && request.ReactiveArmourCue.DeathStarted) ||
                        IsIronDomeAttackerDeath(request, impact))
                        continue;
                    impactView.PlayDeathAnimation();
                    hasAnyDeath = true;
                    deathDuration = Mathf.Max(deathDuration, impactView.GetDeathAnimationDurationSeconds());
                }
                else if (impact.TookDamage)
                {
                    impactView.PlayIdleAnimation();
                }
            }

            var deathWaitDuration = Mathf.Max(_deathHoldDuration, deathDuration);
            if (hasAnyDeath && deathWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(deathWaitDuration);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
            if (request.IronDomeCue != null && _ironDomeMissileVfx != null)
            {
                var remaining = _ironDomeMissileVfx.RemainingDurationSeconds;
                if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
            if (request.ReactiveArmourCue != null && _reactiveArmourVfx != null)
            {
                var remaining = _reactiveArmourVfx.RemainingDurationSeconds;
                if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
            if (request.SpellEffectId == EndTurnService.RedDragonFlamesEffectId && _redDragonEndTurnFlamesVfx != null)
            {
                var remaining = _redDragonEndTurnFlamesVfx.RemainingDurationSeconds;
                if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
            if ((request.SpellEffectId == MeteorRules.EffectId && _meteorVfx != null) ||
                (request.SpellEffectId == FireSpearRules.EffectId && _fireSpearVfx != null))
            {
                var remaining = request.SpellEffectId == FireSpearRules.EffectId
                    ? _fireSpearVfx.RemainingDurationSeconds : _meteorVfx.RemainingDurationSeconds;
                if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
                if (presentationGeneration != _combatPresentationGeneration) yield break;
            }
        }

        private static bool IsIronDomeAttackerDeath(ImpactAnimationRequest request, AttackAnimationImpact impact)
        {
            var cue = request.IronDomeCue;
            return cue != null && impact.Died && impact.OwnerId == cue.AttackerOwnerId &&
                   impact.Coord == cue.AttackerCoord && request.AreaImpactRuntimeIds.TryGetValue(impact, out var runtimeId) &&
                   runtimeId == cue.AttackerRuntimeId;
        }

        private void PlayIronDomeAttackerDeath(ImpactAnimationRequest request)
        {
            var cue = request.IronDomeCue;
            var master = FindTileTextView(cue.MasterOwnerId, cue.MasterCoord);
            if (master == null || !master.isActiveAndEnabled || master.VisualRuntimeId != cue.MasterRuntimeId ||
                master.GetComponent<TileView>()?.CurrentOccupantRuntimeId != cue.MasterRuntimeId) return;
            foreach (var impact in request.Impacts)
            {
                if (!IsIronDomeAttackerDeath(request, impact)) continue;
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null || !MatchesAreaImpactIdentity(request, impact, view) ||
                    view.GetComponent<TileView>()?.CurrentOccupantRuntimeId != cue.AttackerRuntimeId) return;
                view.PlayDeathAnimation();
                request.IronDomeCue.DeathStarted = true;
                request.IronDomeCue.DeathEndsAt = Time.unscaledTime +
                    Mathf.Max(_deathHoldDuration, view.GetDeathAnimationDurationSeconds());
                return;
            }
        }

        private static bool IsReactiveArmourAttackerDeath(ImpactAnimationRequest request, AttackAnimationImpact impact)
        {
            var cue = request.ReactiveArmourCue;
            return cue != null && impact.Died && impact.OwnerId == cue.AttackerOwnerId &&
                   impact.Coord == cue.AttackerCoord && request.AreaImpactRuntimeIds.TryGetValue(impact, out var runtimeId) &&
                   runtimeId == cue.AttackerRuntimeId;
        }

        private void PlayReactiveArmourAttackerDeath(ImpactAnimationRequest request)
        {
            foreach (var impact in request.Impacts)
            {
                if (!IsReactiveArmourAttackerDeath(request, impact)) continue;
                var view = FindTileTextView(impact.OwnerId, impact.Coord);
                if (view == null || !MatchesAreaImpactIdentity(request, impact, view)) return;
                view.PlayDeathAnimation();
                request.ReactiveArmourCue.DeathStarted = true;
                request.ReactiveArmourCue.DeathEndsAt = Time.unscaledTime +
                    Mathf.Max(_deathHoldDuration, view.GetDeathAnimationDurationSeconds());
                return;
            }
        }

        private void ProjectDeferredSpellHp(ImpactAnimationRequest request, bool afterImpact)
        {
            if (request.SpellEffectId != MeteorRules.EffectId && request.SpellEffectId != FireSpearRules.EffectId && request.SpellEffectId != EndTurnService.RedDragonFlamesEffectId) return;
            var values = new Dictionary<string, BattleValuePopupEvent>(StringComparer.Ordinal);
            foreach (var popup in request.ValuePopupEvents)
            {
                if (popup == null || string.IsNullOrWhiteSpace(popup.RuntimeId)) continue;
                if (afterImpact || !values.ContainsKey(popup.RuntimeId)) values[popup.RuntimeId] = popup;
            }
            foreach (var pair in values)
            {
                var popup = pair.Value;
                FindTileTextView(popup.OwnerId, popup.Coord)?.SetPresentedHp(
                    popup.RuntimeId, afterImpact ? popup.HpAfter : popup.HpBefore);
            }
        }

        private IEnumerator AnimateVisualTravel(TileTextView tileTextView, Vector3 fromWorldPosition, Vector3 toWorldPosition, float duration)
        {
            if (tileTextView == null || !tileTextView.isActiveAndEnabled)
            {
                yield break;
            }

            tileTextView.SetVisualWorldPosition(fromWorldPosition);
            if (duration <= 0f)
            {
                tileTextView.SetVisualWorldPosition(toWorldPosition);
                yield break;
            }

            var generation = _combatPresentationGeneration;
            var runtimeId = tileTextView.VisualRuntimeId;
            var startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < duration)
            {
                if (generation != _combatPresentationGeneration || tileTextView == null ||
                    !tileTextView.isActiveAndEnabled || tileTextView.VisualRuntimeId != runtimeId) yield break;
                // deltaTime also includes work before this travel began in the current frame.
                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                tileTextView.SetVisualWorldPosition(Vector3.LerpUnclamped(fromWorldPosition, toWorldPosition, easedProgress));
                yield return null;
            }

            if (generation == _combatPresentationGeneration && tileTextView != null &&
                tileTextView.isActiveAndEnabled && tileTextView.VisualRuntimeId == runtimeId)
                tileTextView.SetVisualWorldPosition(toWorldPosition);
        }

        private static Vector3 GetRangedAttackNudgeTarget(Vector3 attackerStartPosition, Vector3 attackDestination, float nudgeDistance)
        {
            if (nudgeDistance <= 0f)
            {
                return attackerStartPosition;
            }

            var direction = attackDestination - attackerStartPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            else
            {
                direction.Normalize();
            }

            return attackerStartPosition + (direction * nudgeDistance);
        }

        private static Vector3 GetMeleeAttackContactTarget(
            TileTextView destinationView,
            PlayerId defenderOwnerId,
            Vector3 fallbackDestination,
            float contactOffset,
            float sideOffset)
        {
            if (destinationView == null)
            {
                return fallbackDestination;
            }

            var localOffset = defenderOwnerId == PlayerId.AI
                ? new Vector2(-30f, 40f)
                : new Vector2(30f, 40f);

            return destinationView.GetTileAnchorWorldPosition(localOffset);
        }

        private TileTextView FindTileTextView(PlayerId ownerId, TileCoord coord)
        {
            var boardPresenter = _battleScreenPresenter == null ? null : _battleScreenPresenter.BoardPresenter;
            if (boardPresenter == null)
            {
                return null;
            }

            var tileViews = ownerId == PlayerId.Player
                ? boardPresenter.PlayerTileViews
                : boardPresenter.AITileViews;

            if (tileViews == null)
            {
                return null;
            }

            foreach (var tileView in tileViews)
            {
                if (tileView != null && tileView.Coord == coord)
                {
                    return tileView.GetComponent<TileTextView>();
                }
            }

            return null;
        }

        private static BattlePresentationSnapshot CaptureBattleSnapshot(BattleState battleState)
        {
            var snapshot = new BattlePresentationSnapshot();
            if (battleState == null)
            {
                return snapshot;
            }

            CaptureBoardSnapshots(snapshot, battleState.PlayerBoard);
            CaptureBoardSnapshots(snapshot, battleState.AIBoard);
            snapshot.Phase = battleState.Phase;
            snapshot.TimedBombEvents.AddRange(battleState.TimedBombCountdownEvents);
            snapshot.AreaSpellEvents.AddRange(battleState.AreaSpellEffectEvents);
            snapshot.RawValuePopupEvents.AddRange(battleState.ValuePopupEvents);
            return snapshot;
        }

        private static List<BattleValuePopupEvent> CloneValuePopupEvents(BattleState battleState)
        {
            var clonedEvents = new List<BattleValuePopupEvent>();
            if (battleState?.ValuePopupEvents == null)
            {
                return clonedEvents;
            }

            foreach (var valuePopupEvent in battleState.ValuePopupEvents)
            {
                if (valuePopupEvent == null ||
                    (valuePopupEvent.Amount <= 0 && !valuePopupEvent.IsInvinciblePrevented))
                {
                    continue;
                }

                clonedEvents.Add(new BattleValuePopupEvent(
                    valuePopupEvent.RuntimeId,
                    valuePopupEvent.OwnerId,
                    valuePopupEvent.Coord,
                    valuePopupEvent.IsHealing,
                    valuePopupEvent.Amount,
                    valuePopupEvent.SourceOwnerId,
                    valuePopupEvent.SourceRuntimeId,
                    valuePopupEvent.SourceCardId,
                    valuePopupEvent.Cause,
                    valuePopupEvent.DamageType,
                    valuePopupEvent.HpBefore,
                    valuePopupEvent.HpAfter,
                    valuePopupEvent.IsInvinciblePrevented));
            }

            return clonedEvents;
        }

        private static void CaptureBoardSnapshots(BattlePresentationSnapshot snapshot, BoardState boardState)
        {
            if (snapshot == null || boardState == null)
            {
                return;
            }

            foreach (var occupant in boardState.EnumerateOccupants())
            {
                if (occupant == null)
                {
                    continue;
                }

                snapshot.Occupants.Add(new OccupantPresentationSnapshot(
                    occupant.RuntimeId,
                    occupant.OwnerId,
                    occupant.Position,
                    occupant.CurrentHp, occupant.CardId, occupant.IsDemonKingRevivalPending, occupant.Kind));
            }
        }

        private float QueueValuePopupAnimations(IReadOnlyList<BattleValuePopupEvent> valuePopupEvents)
        {
            if (valuePopupEvents == null || valuePopupEvents.Count == 0)
            {
                return 0f;
            }

            var perRuntimeIdDelaySteps = new Dictionary<string, int>(StringComparer.Ordinal);
            var maxScheduledDelay = 0f;
            var anyPopupQueued = false;

            foreach (var valuePopupEvent in valuePopupEvents)
            {
                if (valuePopupEvent == null ||
                    (valuePopupEvent.Amount <= 0 && !valuePopupEvent.IsInvinciblePrevented))
                {
                    continue;
                }

                var targetView = FindTileTextView(valuePopupEvent.OwnerId, valuePopupEvent.Coord);
                if (targetView == null)
                {
                    continue;
                }

                var runtimeId = string.IsNullOrWhiteSpace(valuePopupEvent.RuntimeId)
                    ? $"{valuePopupEvent.OwnerId}:{valuePopupEvent.Coord}"
                    : valuePopupEvent.RuntimeId;
                perRuntimeIdDelaySteps.TryGetValue(runtimeId, out var delayStepIndex);
                var scheduledDelay = delayStepIndex * Mathf.Max(0f, _valuePopupCascadeDelay);
                if (valuePopupEvent.IsInvinciblePrevented)
                {
                    targetView.QueueInvinciblePopup(scheduledDelay, delayStepIndex);
                }
                else
                {
                    targetView.QueueFloatingValuePopup(
                        valuePopupEvent.Amount,
                        valuePopupEvent.IsHealing,
                        scheduledDelay,
                        delayStepIndex);
                }
                perRuntimeIdDelaySteps[runtimeId] = delayStepIndex + 1;
                maxScheduledDelay = Mathf.Max(maxScheduledDelay, scheduledDelay);
                anyPopupQueued = true;
            }

            return anyPopupQueued ? maxScheduledDelay + Mathf.Max(0f, _valuePopupCascadeDelay) : 0f;
        }

        private static AttackAnimationRequest BuildAttackAnimationRequest(
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            PlayerId attackerOwnerId,
            TileCoord attackerCoord,
            PlayerId defenderOwnerId,
            TileCoord declaredTargetCoord,
            ShielderService.ShielderInfo shielderInfo,
            bool attackerIsRanged,
            bool defenderCounterattacks,
            IReadOnlyList<BattleValuePopupEvent> valuePopupEvents)
        {
            if (beforeSnapshot == null)
            {
                return null;
            }

            var attackerBefore = beforeSnapshot.FindByCoord(attackerOwnerId, attackerCoord);
            if (attackerBefore == null)
            {
                return null;
            }

            var attackerAfter = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(attackerBefore.RuntimeId);
            var request = new AttackAnimationRequest(
                attackerOwnerId,
                attackerCoord,
                defenderOwnerId,
                declaredTargetCoord,
                shielderInfo.IsProtected && shielderInfo.ShielderCoord.HasValue
                    ? shielderInfo.ShielderCoord.Value
                    : declaredTargetCoord,
                attackerIsRanged)
            {
                AttackerCardId = attackerBefore.CardId,
                DefenderCardId = beforeSnapshot.FindByCoord(defenderOwnerId,
                    shielderInfo.IsProtected && shielderInfo.ShielderCoord.HasValue
                        ? shielderInfo.ShielderCoord.Value : declaredTargetCoord)?.CardId,
                DefenderCounterattacked = defenderCounterattacks,
                AttackerTookDamage = DidLoseHp(attackerBefore, attackerAfter),
                AttackerDied = DidDie(attackerBefore, attackerAfter),
            };
            request.ValuePopupEvents.AddRange(valuePopupEvents ?? Array.Empty<BattleValuePopupEvent>());

            AddImpactIfNeeded(request, beforeSnapshot, afterSnapshot, shielderInfo.OriginalTarget.OwnerId, shielderInfo.OriginalTargetCoord, shielderInfo.OriginalTarget.RuntimeId);

            if (shielderInfo.IsProtected && shielderInfo.ShielderCoord.HasValue && shielderInfo.Shielder != null)
            {
                AddImpactIfNeeded(request, beforeSnapshot, afterSnapshot, shielderInfo.Shielder.OwnerId, shielderInfo.ShielderCoord.Value, shielderInfo.Shielder.RuntimeId);
            }

            foreach (var valuePopupEvent in valuePopupEvents ?? Array.Empty<BattleValuePopupEvent>())
            {
                if (valuePopupEvent == null ||
                    valuePopupEvent.IsHealing ||
                    valuePopupEvent.Cause != BattleValueChangeCause.Piercing)
                {
                    continue;
                }

                AddImpactIfNeeded(
                    request,
                    beforeSnapshot,
                    afterSnapshot,
                    valuePopupEvent.OwnerId,
                    valuePopupEvent.Coord,
                    valuePopupEvent.RuntimeId);
            }

            return request;
        }

        private static bool IsRangedAttacker(BattleState battleState, PlayerId ownerId, TileCoord attackerCoord)
        {
            if (battleState == null)
            {
                return false;
            }

            var attacker = battleState.GetBoard(ownerId)?.GetOccupant(attackerCoord);
            return attacker != null && attacker.AttackType == AttackType.Ranged;
        }

        private static ImpactAnimationRequest BuildImpactAnimationRequest(
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            IReadOnlyList<BattleValuePopupEvent> valuePopupEvents,
            string sourceCardId = null,
            PlayerId? spellCasterOwnerId = null)
        {
            if (beforeSnapshot == null)
            {
                return null;
            }

            var request = new ImpactAnimationRequest
            {
                SpellEffectId = ResolveDamageSpellEffectId(sourceCardId),
                SpellCasterOwnerId = spellCasterOwnerId,
                GuardAreaRuntimeIds = sourceCardId == EarthquakeRules.CardId || sourceCardId == MeteorRules.CardId || sourceCardId == FireSpearRules.CardId || sourceCardId == TrapMirrorFieldCardRules.CardId || sourceCardId == SummonTrapCardRules.CardId || IsDirectDestructionTrapCard(sourceCardId),
            };
            request.ValuePopupEvents.AddRange(valuePopupEvents ?? Array.Empty<BattleValuePopupEvent>());
            foreach (var before in beforeSnapshot.Occupants)
            {
                if (before == null)
                {
                    continue;
                }

                var after = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(before.RuntimeId);
                // A directly destroyed occupant can disappear with positive HP. Only real
                // damage packets should produce Hurt/UnitHit during this trap's resolution.
                var tookDamage = IsDirectDestructionTrapCard(sourceCardId)
                    ? HasRecordedDamage(valuePopupEvents, before.RuntimeId)
                    : DidLoseHp(before, after);
                var died = DidDie(before, after);
                if ((sourceCardId == EarthquakeRules.CardId || sourceCardId == MeteorRules.CardId || sourceCardId == FireSpearRules.CardId || sourceCardId == TrapMirrorFieldCardRules.CardId || sourceCardId == SummonTrapCardRules.CardId || IsDirectDestructionTrapCard(sourceCardId)) &&
                    after != null && after.IsRevivalPending) died = false;
                if (!tookDamage && !died)
                {
                    continue;
                }

                var impact = new AttackAnimationImpact(before.OwnerId, before.Coord, tookDamage, died);
                request.Impacts.Add(impact);
                if (request.GuardAreaRuntimeIds) request.AreaImpactRuntimeIds[impact] = before.RuntimeId;
            }

            if (beforeSnapshot.Phase == PhaseType.TurnStart && afterSnapshot != null && afterSnapshot.TimedBombEvents.Count > 0)
                BuildTimedBombSteps(request, beforeSnapshot, afterSnapshot);
            if (afterSnapshot != null && afterSnapshot.AreaSpellEvents.Exists(e =>
                !beforeSnapshot.AreaSpellEvents.Contains(e) &&
                (e.EffectId == EndTurnService.RedDragonFlamesEffectId ||
                    (beforeSnapshot.Phase == PhaseType.TurnStart && e.EffectId == FirewallSpellEffectId))))
                BuildAreaSpellSteps(request, beforeSnapshot, afterSnapshot);
            else if (beforeSnapshot.Phase == PhaseType.TurnStart && afterSnapshot != null && request.TimedBombSteps.Count == 0 &&
                (beforeSnapshot.AreaSpellEvents.Exists(e => e.EffectId == FirewallSpellEffectId || e.EffectId == EndTurnService.RedDragonFlamesEffectId) || afterSnapshot.AreaSpellEvents.Exists(e => e.EffectId == FirewallSpellEffectId || e.EffectId == EndTurnService.RedDragonFlamesEffectId)))
            {
                request.ValuePopupEvents.Clear();
                for (var index = Math.Min(beforeSnapshot.RawValuePopupEvents.Count, afterSnapshot.RawValuePopupEvents.Count); index < afterSnapshot.RawValuePopupEvents.Count; index++)
                {
                    var popup = afterSnapshot.RawValuePopupEvents[index];
                    if (popup != null && (popup.Amount > 0 || popup.IsInvinciblePrevented)) request.ValuePopupEvents.Add(popup);
                }
            }
            return request;
        }

        private static void ConfigureDestructionTrapCue(ImpactAnimationRequest request,
            BattlePresentationSnapshot beforeSnapshot, string cardId, PlayerId masterOwnerId,
            PlayerId attackerOwnerId, TileCoord attackerCoord, string attackerRuntimeId)
        {
            if (request == null || beforeSnapshot == null || masterOwnerId == attackerOwnerId ||
                string.IsNullOrWhiteSpace(attackerRuntimeId)) return;
            var attacker = beforeSnapshot.FindByRuntimeId(attackerRuntimeId);
            if (attacker == null || attacker.OwnerId != attackerOwnerId || attacker.Coord != attackerCoord ||
                attacker.Kind != OccupantKind.Unit) return;
            foreach (var master in beforeSnapshot.Occupants)
                if (master.OwnerId == masterOwnerId && master.Kind == OccupantKind.Master)
                {
                    var cue = new DestructionTrapActivationCue(masterOwnerId, master.Coord,
                        master.RuntimeId, attackerOwnerId, attackerCoord, attackerRuntimeId);
                    if (cardId == ExplosiveReactiveArmourCardRules.CardId) request.ReactiveArmourCue = cue;
                    else if (cardId == IronDomeCardRules.CardId) request.IronDomeCue = cue;
                    return;
                }
        }

        private static bool IsDirectDestructionTrapCard(string cardId) =>
            cardId == ExplosiveReactiveArmourCardRules.CardId || cardId == IronDomeCardRules.CardId;

        private static bool HasRecordedDamage(IReadOnlyList<BattleValuePopupEvent> popups, string runtimeId)
        {
            if (popups == null || string.IsNullOrEmpty(runtimeId)) return false;
            foreach (var popup in popups)
                if (popup != null && popup.RuntimeId == runtimeId && !popup.IsHealing &&
                    !popup.IsInvinciblePrevented && popup.Amount > 0) return true;
            return false;
        }

        private static void BuildTimedBombSteps(ImpactAnimationRequest request, BattlePresentationSnapshot before, BattlePresentationSnapshot after)
        {
            var bombPopupIndices = new bool[after.RawValuePopupEvents.Count];
            request.ValuePopupEvents.Clear();
            var freshStart = Math.Min(before.RawValuePopupEvents.Count, after.RawValuePopupEvents.Count);
            var prefixEnd = Math.Min(after.TimedBombEvents[0].ValuePopupEventIndex, after.RawValuePopupEvents.Count);
            var prefix = new ImpactAnimationRequest();
            for (var index = freshStart; index < prefixEnd; index++)
            {
                bombPopupIndices[index] = true;
                var popup = after.RawValuePopupEvents[index];
                if (popup == null || (popup.Amount <= 0 && !popup.IsInvinciblePrevented)) continue;
                prefix.ValuePopupEvents.Add(popup);
                if (popup.IsHealing || popup.IsInvinciblePrevented) continue;
                AddOrMergeImpact(prefix.Impacts, popup.OwnerId, popup.Coord, tookDamage: true,
                    died: popup.HpAfter <= 0 && DidDie(before.FindByRuntimeId(popup.RuntimeId), after.FindByRuntimeId(popup.RuntimeId)));
                request.Impacts.RemoveAll(impact => impact.OwnerId == popup.OwnerId && impact.Coord == popup.Coord);
                var source = before.FindByRuntimeId(popup.SourceRuntimeId);
                if (popup.Cause == BattleValueChangeCause.NuclearPowerPlant && source != null &&
                    DidDie(source, after.FindByRuntimeId(popup.SourceRuntimeId)))
                {
                    AddOrMergeImpact(prefix.Impacts, source.OwnerId, source.Coord, tookDamage: false, died: true);
                    request.Impacts.RemoveAll(impact => impact.OwnerId == source.OwnerId && impact.Coord == source.Coord);
                }
            }
            if (HasPresentationImpact(prefix)) request.TimedBombSteps.Add(prefix);
            for (var i = 0; i < after.TimedBombEvents.Count; i++)
            {
                var cue = after.TimedBombEvents[i];
                var step = new ImpactAnimationRequest
                {
                    TimedBombCountdown = new TimedBombCountdownCue(cue.SourceOwnerId, cue.EffectIndex,
                        cue.RemainingTriggersBefore, cue.RemainingTriggersAfter)
                };
                var end = cue.ValuePopupEventEndIndex;
                for (var index = Math.Max(0, cue.ValuePopupEventIndex); index < end && index < after.RawValuePopupEvents.Count; index++)
                {
                    bombPopupIndices[index] = true;
                    var popup = after.RawValuePopupEvents[index];
                    if (popup == null || (popup.Amount <= 0 && !popup.IsInvinciblePrevented)) continue;
                    step.ValuePopupEvents.Add(popup);
                    if (popup.IsHealing || popup.IsInvinciblePrevented) continue;
                    var previous = before.FindByRuntimeId(popup.RuntimeId);
                    var current = after.FindByRuntimeId(popup.RuntimeId);
                    AddOrMergeImpact(step.Impacts, popup.OwnerId, popup.Coord, tookDamage: true,
                        died: popup.HpAfter <= 0 && DidDie(previous, current));
                    request.Impacts.RemoveAll(impact => impact.OwnerId == popup.OwnerId && impact.Coord == popup.Coord);
                }
                request.TimedBombSteps.Add(step);
            }
            // Callers pass clones. Raw boundaries preserve fresh effects before a bomb,
            // exclude old cached popups, and keep subsequent effects out of its reaction.
            for (var index = freshStart; index < after.RawValuePopupEvents.Count; index++)
            {
                var popup = after.RawValuePopupEvents[index];
                if (!bombPopupIndices[index] && popup != null && (popup.Amount > 0 || popup.IsInvinciblePrevented))
                    request.ValuePopupEvents.Add(popup);
            }
            foreach (var popup in request.ValuePopupEvents)
            {
                if (popup == null || popup.Amount <= 0 || popup.IsHealing || popup.IsInvinciblePrevented) continue;
                AddOrMergeImpact(request.Impacts, popup.OwnerId, popup.Coord, tookDamage: true,
                    died: popup.HpAfter <= 0 && DidDie(before.FindByRuntimeId(popup.RuntimeId), after.FindByRuntimeId(popup.RuntimeId)));
            }
        }

        private static void BuildAreaSpellSteps(ImpactAnimationRequest request, BattlePresentationSnapshot before, BattlePresentationSnapshot after)
        {
            var cues = after.AreaSpellEvents.FindAll(e => !before.AreaSpellEvents.Contains(e));
            if (cues.Count == 0) return;
            var consumed = new bool[after.RawValuePopupEvents.Count];
            // Main-phase commands reset their raw popup list; TurnStart appends to it.
            var freshStart = before.Phase == PhaseType.Main ? 0 : Math.Min(before.RawValuePopupEvents.Count, consumed.Length);
            if (request.TimedBombSteps.Count > 0)
            {
                var firstBomb = Math.Min(after.TimedBombEvents[0].ValuePopupEventIndex, consumed.Length);
                for (var index = freshStart; index < firstBomb; index++) consumed[index] = true;
                foreach (var bomb in after.TimedBombEvents)
                    for (var index = Math.Max(0, bomb.ValuePopupEventIndex); index < bomb.ValuePopupEventEndIndex && index < consumed.Length; index++)
                        consumed[index] = true;
            }
            var prefix = new ImpactAnimationRequest { GuardAreaRuntimeIds = true };
            AppendAreaPopupRange(prefix, request, before, after, consumed, freshStart, cues[0].ValuePopupEventIndex);
            if (HasPresentationImpact(prefix)) request.AreaSpellSteps.Add(prefix);
            for (var cueIndex = 0; cueIndex < cues.Count; cueIndex++)
            {
                var cue = cues[cueIndex];
                var step = new ImpactAnimationRequest { SpellEffectId = ResolveAreaSpellEffectId(cue.EffectId), GuardAreaRuntimeIds = true };
                foreach (var coord in cue.TargetCoords) step.SpellEffectTargets.Add(new SpellEffectTarget(cue.TargetOwnerId, coord));
                var end = cueIndex + 1 < cues.Count ? cues[cueIndex + 1].ValuePopupEventIndex : consumed.Length;
                AppendAreaPopupRange(step, request, before, after, consumed, Math.Max(freshStart, cue.ValuePopupEventIndex), end);
                request.AreaSpellSteps.Add(step);
            }
            // These are raw offsets, not clone identities. Old cached packets and packets
            // already owned by a bomb or an area cue never reach the parent's reaction.
            request.ValuePopupEvents.Clear();
            for (var index = freshStart; index < consumed.Length; index++)
            {
                var popup = after.RawValuePopupEvents[index];
                if (!consumed[index] && popup != null && (popup.Amount > 0 || popup.IsInvinciblePrevented)) request.ValuePopupEvents.Add(popup);
            }
        }
        private static void AppendAreaPopupRange(ImpactAnimationRequest step, ImpactAnimationRequest parent,
            BattlePresentationSnapshot before, BattlePresentationSnapshot after, bool[] consumed, int start, int end)
        {
            for (var index = Math.Max(0, start); index < Math.Min(end, consumed.Length); index++)
            {
                if (consumed[index]) continue;
                consumed[index] = true;
                var popup = after.RawValuePopupEvents[index];
                if (popup == null || (popup.Amount <= 0 && !popup.IsInvinciblePrevented)) continue;
                step.ValuePopupEvents.Add(popup);
                if (popup.IsHealing || popup.IsInvinciblePrevented) continue;
                var current = after.FindByRuntimeId(popup.RuntimeId);
                var died = popup.HpBefore > 0 && popup.HpAfter <= 0 &&
                    (current == null || (current.CurrentHp <= 0 && !current.IsRevivalPending));
                AddOrMergeImpact(step.Impacts, popup.OwnerId, popup.Coord, tookDamage: true, died: died);
                foreach (var impact in step.Impacts)
                    if (impact.OwnerId == popup.OwnerId && impact.Coord == popup.Coord) step.AreaImpactRuntimeIds[impact] = popup.RuntimeId;
                parent.Impacts.RemoveAll(impact => impact.OwnerId == popup.OwnerId && impact.Coord == popup.Coord);
                var source = before.FindByRuntimeId(popup.SourceRuntimeId);
                if (popup.Cause == BattleValueChangeCause.NuclearPowerPlant && source != null && DidDie(source, after.FindByRuntimeId(source.RuntimeId)))
                {
                    AddOrMergeImpact(step.Impacts, source.OwnerId, source.Coord, tookDamage: false, died: true);
                    foreach (var impact in step.Impacts)
                        if (impact.OwnerId == source.OwnerId && impact.Coord == source.Coord) step.AreaImpactRuntimeIds[impact] = source.RuntimeId;
                    parent.Impacts.RemoveAll(impact => impact.OwnerId == source.OwnerId && impact.Coord == source.Coord);
                }
            }
        }
        private static bool MatchesAreaImpactIdentity(ImpactAnimationRequest request, AttackAnimationImpact impact, TileTextView view)
        {
            return !request.AreaImpactRuntimeIds.TryGetValue(impact, out var runtimeId) ||
                (view != null && view.isActiveAndEnabled && view.VisualRuntimeId == runtimeId);
        }

        private static bool HasPresentationImpact(ImpactAnimationRequest request)
        {
            if (request == null)
            {
                return false;
            }

            return request.Impacts.Count > 0 ||
                   request.ValuePopupEvents.Count > 0 ||
                   request.ReactiveArmourCue != null || request.IronDomeCue != null || request.TimedBombCountdown != null || request.TimedBombSteps.Count > 0 || request.AreaSpellSteps.Count > 0 ||
                   !string.IsNullOrWhiteSpace(request.SpellEffectId);
        }

        private IEnumerator PlaySpellEffectAnimationSequence(ImpactAnimationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SpellEffectId))
            {
                yield break;
            }

            if (request.SpellEffectId == EndTurnService.RedDragonFlamesEffectId)
            {
                var duration = PlayRedDragonEndTurnFlames(request);
                if (duration > 0f) yield return new WaitForSecondsRealtime(RedDragonEndTurnFlamesVfx.ImpactDelaySeconds);
                yield break;
            }
            if (request.SpellEffectId == MeteorRules.EffectId)
            {
                if (!request.SpellCasterOwnerId.HasValue) yield break;
                var generation = _combatPresentationGeneration;
                var duration = PlayMeteor(request.SpellCasterOwnerId.Value);
                if (duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(MeteorVfx.ImpactDelaySeconds);
                    if (generation != _combatPresentationGeneration) yield break;
                    _meteorVfx?.TryBeginImpact();
                }
                yield break;
            }
            if (request.SpellEffectId == FireSpearRules.EffectId)
            {
                var generation = _combatPresentationGeneration;
                var duration = PlayFireSpear(request);
                if (duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(FireSpearVfx.ImpactDelaySeconds);
                    if (generation != _combatPresentationGeneration) yield break;
                    _fireSpearVfx?.TryBeginImpact();
                }
                yield break;
            }
            if (request.SpellEffectId == PlasmaShieldRules.EffectId)
            {
                var generation = _combatPresentationGeneration;
                var duration = PlayPlasmaShield(request);
                if (duration > 0f)
                    while (generation == _combatPresentationGeneration && _plasmaShieldVfx != null && _plasmaShieldVfx.IsPlaying)
                        yield return null;
                yield break;
            }
            if (request.SpellEffectId == ManaShieldRules.EffectId)
            {
                var generation = _combatPresentationGeneration;
                var duration = PlayManaShield(request);
                if (duration > 0f)
                    while (generation == _combatPresentationGeneration && _manaShieldVfx != null && _manaShieldVfx.IsPlaying)
                        yield return null;
                yield break;
            }
            if (request.SpellEffectId == EarthquakeRules.EffectId)
            {
                if (!request.SpellCasterOwnerId.HasValue) yield break;
                var duration = PlayEarthquake(request.SpellCasterOwnerId.Value);
                if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
                yield break;
            }
            if (request.SpellEffectId == NuclearRules.EffectId)
            {
                if (!request.SpellCasterOwnerId.HasValue) yield break;
                var duration = PlayNuclearExplosion(request.SpellCasterOwnerId.Value);
                if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
                yield break;
            }
            if (request.SpellEffectId == FirewallSpellEffectId)
            {
                var duration = PlayFirewall(request);
                if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
                yield break;
            }

            var effectFrames = GetSpellEffectFrames(request.SpellEffectId);
            if (effectFrames == null || effectFrames.Count == 0)
            {
                yield break;
            }

            var framesPerSecond = GetSpellEffectFramesPerSecond(request.SpellEffectId);
            var effectSize = GetSpellEffectSize(request.SpellEffectId);
            var effectDuration = 0f;

            if (request.SpellEffectTargets.Count > 0)
            {
                foreach (var target in request.SpellEffectTargets)
                {
                    var targetView = FindTileTextView(target.OwnerId, target.Coord);
                    if (targetView == null)
                    {
                        continue;
                    }

                    effectDuration = Mathf.Max(
                        effectDuration,
                        targetView.PlayTransientSpriteEffect(effectFrames, framesPerSecond, effectSize));
                }
            }
            else
            {
                foreach (var impact in request.Impacts)
                {
                    if (!impact.TookDamage && !impact.Died)
                    {
                        continue;
                    }

                    var impactView = FindTileTextView(impact.OwnerId, impact.Coord);
                    if (impactView == null)
                    {
                        continue;
                    }

                    effectDuration = Mathf.Max(
                        effectDuration,
                        impactView.PlayTransientSpriteEffect(effectFrames, framesPerSecond, effectSize));
                }
            }

            if (effectDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(effectDuration);
            }
        }

        private static string ResolveDamageSpellEffectId(string sourceCardId)
        {
            if (string.IsNullOrWhiteSpace(sourceCardId))
            {
                return null;
            }

            if (sourceCardId == NuclearRules.CardId) return NuclearRules.EffectId;
            if (sourceCardId == EarthquakeRules.CardId) return EarthquakeRules.EffectId;
            if (sourceCardId == MeteorRules.CardId) return MeteorRules.EffectId;
            if (sourceCardId == FireSpearRules.CardId) return FireSpearRules.EffectId;
            if (sourceCardId == PlasmaShieldRules.CardId) return PlasmaShieldRules.EffectId;
            if (sourceCardId == ManaShieldRules.CardId) return ManaShieldRules.EffectId;

            return sourceCardId.IndexOf("firebolt", StringComparison.OrdinalIgnoreCase) >= 0
                ? FireboltSpellEffectId
                : null;
        }

        private static string ResolveAreaSpellEffectId(string effectId)
        {
            if (string.Equals(effectId, EndTurnService.RedDragonFlamesEffectId, StringComparison.OrdinalIgnoreCase))
                return EndTurnService.RedDragonFlamesEffectId;
            if (string.Equals(effectId, FirewallSpellEffectId, StringComparison.OrdinalIgnoreCase))
            {
                return FirewallSpellEffectId;
            }

            return string.Equals(effectId, BiochemicalBombSpellEffectId, StringComparison.OrdinalIgnoreCase)
                ? BiochemicalBombSpellEffectId
                : null;
        }

        private IReadOnlyList<Sprite> GetSpellEffectFrames(string spellEffectId)
        {
            if (string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal))
            {
                return GetFireboltSpellEffectFrames();
            }

            if (string.Equals(spellEffectId, FirewallSpellEffectId, StringComparison.Ordinal))
            {
                return HasConfiguredFrames(_firewallTileEffectFrames)
                    ? _firewallTileEffectFrames
                    : LoadSortedEffectFrames(
                        FirewallSpellEffectResourcePath,
                        ref s_firewallSpellEffectFrames);
            }

            if (string.Equals(spellEffectId, BiochemicalBombSpellEffectId, StringComparison.Ordinal))
            {
                return HasConfiguredFrames(_biochemicalBombTileEffectFrames)
                    ? _biochemicalBombTileEffectFrames
                    : LoadSortedEffectFrames(
                        BiochemicalBombSpellEffectResourcePath,
                        ref s_biochemicalBombSpellEffectFrames);
            }

            return Array.Empty<Sprite>();
        }

        private float GetSpellEffectFramesPerSecond(string spellEffectId)
        {
            if (string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal))
            {
                return FireboltSpellEffectFramesPerSecond;
            }

            if (string.Equals(spellEffectId, FirewallSpellEffectId, StringComparison.Ordinal))
            {
                return Mathf.Max(0.01f, _firewallTileEffectFramesPerSecond);
            }

            return string.Equals(spellEffectId, BiochemicalBombSpellEffectId, StringComparison.Ordinal)
                ? Mathf.Max(0.01f, _biochemicalBombTileEffectFramesPerSecond)
                : 12f;
        }

        private Vector2 GetSpellEffectSize(string spellEffectId)
        {
            if (string.Equals(spellEffectId, FireboltSpellEffectId, StringComparison.Ordinal))
            {
                return FireboltSpellEffectSize;
            }

            if (string.Equals(spellEffectId, FirewallSpellEffectId, StringComparison.Ordinal))
            {
                return _firewallTileEffectSize;
            }

            return string.Equals(spellEffectId, BiochemicalBombSpellEffectId, StringComparison.Ordinal)
                ? _biochemicalBombTileEffectSize
                : new Vector2(180f, 180f);
        }

        private float GetSpellEffectDuration(string spellEffectId)
        {
            if (spellEffectId == EndTurnService.RedDragonFlamesEffectId) return RedDragonEndTurnFlamesVfx.DurationSeconds;
            if (spellEffectId == NuclearRules.EffectId) return NuclearExplosionVfx.DurationSeconds;
            if (spellEffectId == EarthquakeRules.EffectId) return EarthquakeVfx.DurationSeconds;
            if (spellEffectId == MeteorRules.EffectId) return MeteorVfx.DurationSeconds;
            if (spellEffectId == FireSpearRules.EffectId) return FireSpearVfx.DurationSeconds;
            if (spellEffectId == PlasmaShieldRules.EffectId) return PlasmaShieldVfx.DurationSeconds;
            if (spellEffectId == ManaShieldRules.EffectId) return ManaShieldVfx.DurationSeconds;
            if (spellEffectId == FirewallSpellEffectId) return FirewallVfx.DurationSeconds;
            if (string.IsNullOrWhiteSpace(spellEffectId))
            {
                return 0f;
            }

            var frames = GetSpellEffectFrames(spellEffectId);
            return frames == null || frames.Count == 0
                ? 0f
                : frames.Count / GetSpellEffectFramesPerSecond(spellEffectId);
        }

        private static bool HasConfiguredFrames(IReadOnlyList<Sprite> frames)
        {
            return frames != null && frames.Count > 0;
        }

        private static IReadOnlyList<Sprite> LoadSortedEffectFrames(
            string resourcePath,
            ref IReadOnlyList<Sprite> cache)
        {
            if (cache != null && cache.Count > 0)
            {
                return cache;
            }

            var importedSprites = Resources.LoadAll<Sprite>(resourcePath);
            if (importedSprites == null || importedSprites.Length == 0)
            {
                cache = Array.Empty<Sprite>();
                return cache;
            }

            Array.Sort(importedSprites, static (left, right) => string.CompareOrdinal(left.name, right.name));
            cache = importedSprites;
            return cache;
        }

        private static IReadOnlyList<Sprite> GetFireboltSpellEffectFrames()
        {
            if (s_fireboltSpellEffectFrames != null && s_fireboltSpellEffectFrames.Count > 0)
            {
                return s_fireboltSpellEffectFrames;
            }

            var importedSprites = Resources.LoadAll<Sprite>(FireboltSpellEffectResourcePath);
            if (importedSprites != null && importedSprites.Length > 0)
            {
                Array.Sort(importedSprites, static (left, right) => string.CompareOrdinal(left.name, right.name));
                s_fireboltSpellEffectFrames = importedSprites;
                return s_fireboltSpellEffectFrames;
            }

            var texture = Resources.Load<Texture2D>(FireboltSpellEffectResourcePath);
            if (texture == null)
            {
                s_fireboltSpellEffectFrames = Array.Empty<Sprite>();
                return s_fireboltSpellEffectFrames;
            }

            var frameCount = Mathf.Max(1, FireboltSpellEffectFrameCount);
            var generatedSprites = new Sprite[frameCount];
            var frameWidth = texture.width / frameCount;
            var frameHeight = texture.height;
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frameRect = new Rect(frameIndex * frameWidth, 0f, frameWidth, frameHeight);
                generatedSprites[frameIndex] = Sprite.Create(
                    texture,
                    frameRect,
                    new Vector2(0.5f, 0.5f),
                    frameHeight,
                    0,
                    SpriteMeshType.FullRect);
                generatedSprites[frameIndex].name = $"FireboltEffect_{frameIndex:D2}";
            }

            s_fireboltSpellEffectFrames = generatedSprites;
            return s_fireboltSpellEffectFrames;
        }

        private static bool CanDefenderCounterattack(
            BattleState battleState,
            PlayerId attackerOwnerId,
            TileCoord attackerCoord,
            PlayerId defenderOwnerId,
            ShielderService.ShielderInfo shielderInfo)
        {
            if (battleState == null)
            {
                return false;
            }

            var attacker = battleState.GetBoard(attackerOwnerId)?.GetOccupant(attackerCoord);
            if (attacker == null || attacker.AttackType != AttackType.Melee)
            {
                return false;
            }

            var actualDefenderCoord = shielderInfo.IsProtected && shielderInfo.ShielderCoord.HasValue
                ? shielderInfo.ShielderCoord.Value
                : shielderInfo.OriginalTargetCoord;

            var defender = battleState.GetBoard(defenderOwnerId)?.GetOccupant(actualDefenderCoord);
            return defender != null &&
                   defender.AttackType == AttackType.Melee &&
                   !defender.CannotCounterattack &&
                   defender.Attack > 0;
        }

        private static void AddImpactIfNeeded(
            AttackAnimationRequest request,
            BattlePresentationSnapshot beforeSnapshot,
            BattlePresentationSnapshot afterSnapshot,
            PlayerId ownerId,
            TileCoord coord,
            string runtimeId)
        {
            if (request == null || beforeSnapshot == null || string.IsNullOrWhiteSpace(runtimeId))
            {
                return;
            }

            var before = beforeSnapshot.FindByRuntimeId(runtimeId) ?? beforeSnapshot.FindByCoord(ownerId, coord);
            if (before == null)
            {
                return;
            }

            var after = afterSnapshot == null ? null : afterSnapshot.FindByRuntimeId(runtimeId);
            var tookDamage = DidLoseHp(before, after);
            var died = DidDie(before, after);
            if (!tookDamage && !died)
            {
                return;
            }

            request.DefenderImpacts.Add(new AttackAnimationImpact(ownerId, coord, tookDamage, died));
        }

        private static bool DidLoseHp(OccupantPresentationSnapshot before, OccupantPresentationSnapshot after)
        {
            if (before == null)
            {
                return false;
            }

            var afterHp = after == null ? 0 : after.CurrentHp;
            return afterHp < before.CurrentHp;
        }

        private static bool DidDie(OccupantPresentationSnapshot before, OccupantPresentationSnapshot after)
        {
            return before != null && before.CurrentHp > 0 && (after == null || after.CurrentHp <= 0);
        }

        private sealed class BattlePresentationSnapshot
        {
            public PhaseType Phase { get; set; }
            public List<BattleTimedBombCountdownEvent> TimedBombEvents { get; } = new List<BattleTimedBombCountdownEvent>();
            public List<BattleAreaSpellEffectEvent> AreaSpellEvents { get; } = new List<BattleAreaSpellEffectEvent>();
            public List<BattleValuePopupEvent> RawValuePopupEvents { get; } = new List<BattleValuePopupEvent>();
            public List<OccupantPresentationSnapshot> Occupants { get; } = new List<OccupantPresentationSnapshot>();

            public OccupantPresentationSnapshot FindByRuntimeId(string runtimeId)
            {
                if (string.IsNullOrWhiteSpace(runtimeId))
                {
                    return null;
                }

                foreach (var occupant in Occupants)
                {
                    if (occupant.RuntimeId == runtimeId)
                    {
                        return occupant;
                    }
                }

                return null;
            }

            public OccupantPresentationSnapshot FindByCoord(PlayerId ownerId, TileCoord coord)
            {
                foreach (var occupant in Occupants)
                {
                    if (occupant.OwnerId == ownerId && occupant.Coord == coord)
                    {
                        return occupant;
                    }
                }

                return null;
            }
        }

        private sealed class OccupantPresentationSnapshot
        {
            public OccupantPresentationSnapshot(string runtimeId, PlayerId ownerId, TileCoord coord, int currentHp, string cardId = null, bool isRevivalPending = false, OccupantKind kind = OccupantKind.Unit)
            {
                RuntimeId = runtimeId;
                OwnerId = ownerId;
                Coord = coord;
                CurrentHp = currentHp;
                CardId = cardId;
                IsRevivalPending = isRevivalPending;
                Kind = kind;
            }

            public OccupantKind Kind { get; }
            public string CardId { get; }
            public string RuntimeId { get; }
            public PlayerId OwnerId { get; }
            public TileCoord Coord { get; }
            public int CurrentHp { get; }
            public bool IsRevivalPending { get; }
        }

        private sealed class AttackAnimationRequest
        {
            public AttackAnimationRequest(
                PlayerId attackerOwnerId,
                TileCoord attackerCoord,
                PlayerId defenderOwnerId,
                TileCoord declaredTargetCoord,
                TileCoord travelTargetCoord,
                bool isRangedAttacker)
            {
                AttackerOwnerId = attackerOwnerId;
                AttackerCoord = attackerCoord;
                DefenderOwnerId = defenderOwnerId;
                DeclaredTargetCoord = declaredTargetCoord;
                TravelTargetCoord = travelTargetCoord;
                IsRangedAttacker = isRangedAttacker;
                DefenderImpacts = new List<AttackAnimationImpact>();
                ValuePopupEvents = new List<BattleValuePopupEvent>();
            }

            public PlayerId AttackerOwnerId { get; }
            public TileCoord AttackerCoord { get; }
            public PlayerId DefenderOwnerId { get; }
            public TileCoord DeclaredTargetCoord { get; }
            public TileCoord TravelTargetCoord { get; }
            public string AttackerCardId { get; set; }
            public string DefenderCardId { get; set; }
            public bool IsRangedAttacker { get; }
            public bool DefenderCounterattacked { get; set; }
            public bool AttackerTookDamage { get; set; }
            public bool AttackerDied { get; set; }
            public List<AttackAnimationImpact> DefenderImpacts { get; }
            public List<BattleValuePopupEvent> ValuePopupEvents { get; }
        }

        private sealed class DestructionTrapActivationCue
        {
            public DestructionTrapActivationCue(PlayerId masterOwnerId, TileCoord masterCoord, string masterRuntimeId,
                PlayerId attackerOwnerId, TileCoord attackerCoord, string attackerRuntimeId)
            {
                MasterOwnerId = masterOwnerId; MasterCoord = masterCoord; MasterRuntimeId = masterRuntimeId;
                AttackerOwnerId = attackerOwnerId; AttackerCoord = attackerCoord; AttackerRuntimeId = attackerRuntimeId;
            }
            public PlayerId MasterOwnerId { get; }
            public TileCoord MasterCoord { get; }
            public string MasterRuntimeId { get; }
            public PlayerId AttackerOwnerId { get; }
            public TileCoord AttackerCoord { get; }
            public string AttackerRuntimeId { get; }
            public bool DeathStarted { get; set; }
            public float DeathEndsAt { get; set; }
        }

        private sealed class ImpactAnimationRequest
        {
            public OccupantState SummonedVisual { get; set; }
            public DestructionTrapActivationCue ReactiveArmourCue { get; set; }
            public DestructionTrapActivationCue IronDomeCue { get; set; }
            public TimedBombCountdownCue TimedBombCountdown { get; set; }
            public List<ImpactAnimationRequest> TimedBombSteps { get; } = new List<ImpactAnimationRequest>();
            public List<ImpactAnimationRequest> AreaSpellSteps { get; } = new List<ImpactAnimationRequest>();
            public Dictionary<AttackAnimationImpact, string> AreaImpactRuntimeIds { get; } = new Dictionary<AttackAnimationImpact, string>();
            public bool GuardAreaRuntimeIds { get; set; }
            public List<AttackAnimationImpact> Impacts { get; } = new List<AttackAnimationImpact>();
            public List<BattleValuePopupEvent> ValuePopupEvents { get; } = new List<BattleValuePopupEvent>();
            public List<SpellEffectTarget> SpellEffectTargets { get; } = new List<SpellEffectTarget>();

            public string SpellTargetRuntimeId { get; set; }
            public string SpellEffectId { get; set; }
            public PlayerId? SpellCasterOwnerId { get; set; }
        }

        private sealed class TimedBombCountdownCue
        {
            public TimedBombCountdownCue(PlayerId caster, int effectIndex, int before, int after)
            { Caster = caster; EffectIndex = effectIndex; Before = before; After = after; }
            public PlayerId Caster { get; }
            public int EffectIndex { get; }
            public int Before { get; }
            public int After { get; }
        }

        private sealed class SpellEffectTarget
        {
            public SpellEffectTarget(PlayerId ownerId, TileCoord coord)
            {
                OwnerId = ownerId;
                Coord = coord;
            }

            public PlayerId OwnerId { get; }

            public TileCoord Coord { get; }
        }

        private sealed class AttackAnimationImpact
        {
            public AttackAnimationImpact(PlayerId ownerId, TileCoord coord, bool tookDamage, bool died)
            {
                OwnerId = ownerId;
                Coord = coord;
                TookDamage = tookDamage;
                Died = died;
            }

            public PlayerId OwnerId { get; }
            public TileCoord Coord { get; }
            public bool TookDamage { get; }
            public bool Died { get; }
        }

        private sealed class OpponentPlayedCardRevealRequest
        {
            public OpponentPlayedCardRevealRequest(string cardId, int cardAttack, int cardMaxHp, int? cardSpellDamage, bool isHiddenTrap = false)
            {
                IsHiddenTrap = isHiddenTrap;
                CardId = isHiddenTrap ? string.Empty : cardId ?? string.Empty;
                CardAttack = Mathf.Max(0, cardAttack);
                CardMaxHp = Mathf.Max(0, cardMaxHp);
                CardSpellDamage = cardSpellDamage;
            }

            public string CardId { get; }
            public int CardAttack { get; }
            public int CardMaxHp { get; }
            public int? CardSpellDamage { get; }
            public bool IsHiddenTrap { get; }
        }
    }
}
