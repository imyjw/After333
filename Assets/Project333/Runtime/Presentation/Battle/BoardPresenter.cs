using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using UnityEngine;

namespace Project333.Runtime.Presentation.Battle
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _playerBoardRoot;
        [SerializeField] private Transform _aiBoardRoot;
        [SerializeField] private TileView[] _playerTileViews = Array.Empty<TileView>();
        [SerializeField] private TileView[] _aiTileViews = Array.Empty<TileView>();

        public TileView[] PlayerTileViews => _playerTileViews;

        public TileView[] AITileViews => _aiTileViews;

        private void Awake()
        {
            RebuildTileRegistry();
        }

        public void RebuildTileRegistry()
        {
            _playerTileViews = CollectDirectChildTileViews(_playerBoardRoot);
            _aiTileViews = CollectDirectChildTileViews(_aiBoardRoot);

            ConfigureTileViews(_playerTileViews, PlayerId.Player);
            ConfigureTileViews(_aiTileViews, PlayerId.AI);
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

        private static void ConfigureTileViews(TileView[] tileViews, PlayerId ownerId)
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
