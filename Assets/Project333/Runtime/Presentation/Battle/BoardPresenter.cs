using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using TMPro;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _playerBoardRoot;
        [SerializeField] private Transform _aiBoardRoot;
        [SerializeField] private TileView[] _playerTileViews = Array.Empty<TileView>();
        [SerializeField] private TileView[] _aiTileViews = Array.Empty<TileView>();

        [Header("Responsive Board Layout")]
        [SerializeField] private bool _useResponsiveLayout = true;
        [SerializeField] private bool _respectSafeArea = true;
        [Range(0.1f, 1f)]
        [SerializeField] private float _safeAreaHeightUsage = 0.9f;
        [Min(0f)]
        [SerializeField] private float _outerHorizontalPadding = 24f;
        [Min(0f)]
        [SerializeField] private float _centerGap = 48f;
        [Min(0f)]
        [SerializeField] private float _boardOutwardOffset = 170f;
        [SerializeField] private Vector2 _tileGap = new Vector2(8f, 8f);
        [Min(0.1f)]
        [SerializeField] private float _tileAspectRatio = 1f;

        [Header("Occupant Stat Bar")]
        [Tooltip("All summoned occupant ATK/HP labels use this TMP font. Leave empty to use each tile's existing font.")]
        [SerializeField] private TMP_FontAsset _occupantStatFont;

        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private Rect _lastSafeArea;
        private bool _hasAppliedResponsiveLayout;

        public TileView[] PlayerTileViews => _playerTileViews;

        public TileView[] AITileViews => _aiTileViews;

        private void Awake()
        {
            RebuildTileRegistry();
            ApplyResponsiveLayout(true);
        }

        private void LateUpdate()
        {
            ApplyResponsiveLayout(false);
        }

        private void OnValidate()
        {
            _safeAreaHeightUsage = Mathf.Clamp(_safeAreaHeightUsage, 0.1f, 1f);
            _outerHorizontalPadding = Mathf.Max(0f, _outerHorizontalPadding);
            _centerGap = Mathf.Max(0f, _centerGap);
            _boardOutwardOffset = Mathf.Max(0f, _boardOutwardOffset);
            _tileGap = new Vector2(Mathf.Max(0f, _tileGap.x), Mathf.Max(0f, _tileGap.y));
            _tileAspectRatio = Mathf.Max(0.1f, _tileAspectRatio);

            if (!UnityEngine.Application.isPlaying)
            {
                RebuildTileRegistry();
                ApplyResponsiveLayout(true);
            }
        }

        public void RebuildTileRegistry()
        {
            _playerTileViews = CollectDirectChildTileViews(_playerBoardRoot);
            _aiTileViews = CollectDirectChildTileViews(_aiBoardRoot);

            ConfigureTileViews(_playerTileViews, PlayerId.Player, _occupantStatFont);
            ConfigureTileViews(_aiTileViews, PlayerId.AI, _occupantStatFont);
        }

        [ContextMenu("Apply Responsive Board Layout")]
        public void ApplyResponsiveLayoutNow()
        {
            RebuildTileRegistry();
            ApplyResponsiveLayout(true);
        }

        public void Present(BattleState battleState)
        {
            EnsureTileRegistry();

            PresentBoard(
                battleState == null ? null : battleState.PlayerBoard,
                _playerTileViews);

            PresentBoard(
                battleState == null ? null : battleState.AIBoard,
                _aiTileViews);
        }

        private void EnsureTileRegistry()
        {
            if (_playerTileViews.Length == 0 && _playerBoardRoot != null)
            {
                RebuildTileRegistry();
            }

            if (_aiTileViews.Length == 0 && _aiBoardRoot != null)
            {
                RebuildTileRegistry();
            }
        }

        private void ApplyResponsiveLayout(bool force)
        {
            if (!_useResponsiveLayout || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = _respectSafeArea
                ? Screen.safeArea
                : new Rect(0f, 0f, Screen.width, Screen.height);
            if (!force &&
                _hasAppliedResponsiveLayout &&
                _lastScreenWidth == Screen.width &&
                _lastScreenHeight == Screen.height &&
                _lastSafeArea == safeArea)
            {
                return;
            }

            EnsureTileRegistry();
            var layout = BoardResponsiveLayoutCalculator.Calculate(
                Screen.width,
                Screen.height,
                safeArea,
                _safeAreaHeightUsage,
                _outerHorizontalPadding,
                _centerGap,
                _tileGap,
                _tileAspectRatio,
                _boardOutwardOffset);

            ApplyResponsiveLayout(_playerTileViews, PlayerId.Player, layout);
            ApplyResponsiveLayout(_aiTileViews, PlayerId.AI, layout);

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = safeArea;
            _hasAppliedResponsiveLayout = true;
        }

        private static void ApplyResponsiveLayout(
            TileView[] tileViews,
            PlayerId ownerId,
            BoardResponsiveLayout layout)
        {
            if (tileViews == null)
            {
                return;
            }

            for (var i = 0; i < tileViews.Length; i++)
            {
                var tileView = tileViews[i];
                if (tileView == null)
                {
                    continue;
                }

                var coord = tileView.Coord;
                var tileCenter = layout.GetTileCenter(ownerId, coord);
                var tileTextView = tileView.GetComponent<TileTextView>();
                tileTextView?.ApplyResponsiveTileLayout(tileCenter, layout.TileSize);
            }
        }

        private static TileView[] CollectDirectChildTileViews(Transform root)
        {
            if (root == null)
            {
                return Array.Empty<TileView>();
            }

            var tileViews = new TileView[root.childCount];
            for (var i = 0; i < root.childCount; i++)
            {
                tileViews[i] = root.GetChild(i).GetComponent<TileView>();
            }

            return tileViews;
        }

        private static void ConfigureTileViews(
            TileView[] tileViews,
            PlayerId ownerId,
            TMP_FontAsset occupantStatFont)
        {
            var maxCount = Math.Min(tileViews.Length, BoardPresenterLayout.TilesPerSide);

            for (var i = 0; i < maxCount; i++)
            {
                var tileView = tileViews[i];
                if (tileView == null)
                {
                    continue;
                }

                var coord = BoardPresenterLayout.GetCoordForIndex(i);
                tileView.Configure(ownerId, coord.Column, coord.Row);
                tileView.GetComponent<TileTextView>()?.ConfigureOccupantStatFont(occupantStatFont);
            }
        }

        private static void PresentBoard(BoardState boardState, TileView[] tileViews)
        {
            foreach (var tileView in tileViews)
            {
                if (tileView == null)
                {
                    continue;
                }

                var occupant = boardState == null
                    ? null
                    : boardState.GetOccupant(tileView.Coord);

                tileView.Present(occupant);
            }
        }
    }
}
