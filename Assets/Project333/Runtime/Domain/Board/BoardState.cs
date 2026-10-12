using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Board
{
    public sealed class BoardState
    {
        public const int ColumnCount = 5;
        public const int RowCount = 2;

        private readonly OccupantState[,] _tiles = new OccupantState[ColumnCount, RowCount];
        private bool _refreshingOrcChieftainAuras;
        private ResourceSet _playerResources = null!;
        private ResourceSet _aiResources = null!;

        // Battle construction rebinds these to that state's own resources after copy/restore.
        internal void BindOwnerResources(ResourceSet playerResources, ResourceSet aiResources)
        {
            _playerResources = playerResources;
            _aiResources = aiResources;
        }

        internal int GetOwnerGold(PlayerId ownerId) =>
            (ownerId == PlayerId.Player ? _playerResources : _aiResources)?.Gold ?? 0;

        // Retained as a cast counter for older snapshots; never grants exposure on placement.
        public int FalloutStacks { get; private set; }

        public void RestoreFalloutStacks(int stacks) => FalloutStacks = Math.Max(0, stacks);

        public void AddFalloutStack()
        {
            FalloutStacks++;
            foreach (var occupant in EnumerateOccupants()) occupant.AddFalloutStack();
        }

        public bool IsInside(TileCoord coord)
        {
            return coord.Column >= 0 &&
                   coord.Column < ColumnCount &&
                   coord.Row >= 0 &&
                   coord.Row < RowCount;
        }

        public bool IsEmpty(TileCoord coord)
        {
            return GetOccupant(coord) == null;
        }

        public OccupantState GetOccupant(TileCoord coord)
        {
            if (!IsInside(coord))
            {
                throw new ArgumentOutOfRangeException(nameof(coord), "Tile coordinate is outside the board.");
            }

            return _tiles[coord.Column, coord.Row];
        }

        public void Place(TileCoord coord, OccupantState occupant, bool synchronizeAuras = true)
        {
            if (!IsInside(coord))
            {
                throw new ArgumentOutOfRangeException(nameof(coord), "Tile coordinate is outside the board.");
            }

            if (_tiles[coord.Column, coord.Row] != null)
            {
                throw new InvalidOperationException("Cannot place an occupant on a non-empty tile.");
            }

            _tiles[coord.Column, coord.Row] = occupant;
            occupant.Position = coord;
            occupant.AttachToBoard(this);
            if (synchronizeAuras) RefreshOrcChieftainAuras();
        }

        public OccupantState Remove(TileCoord coord, bool synchronizeAuras = true)
        {
            if (!IsInside(coord))
            {
                throw new ArgumentOutOfRangeException(nameof(coord), "Tile coordinate is outside the board.");
            }

            var occupant = _tiles[coord.Column, coord.Row];
            _tiles[coord.Column, coord.Row] = null;
            occupant?.DetachFromBoard(this);
            if (synchronizeAuras)
            {
                occupant?.ReconcileOrcChieftainAuras(Array.Empty<OccupantState>());
                RefreshOrcChieftainAuras();
            }
            return occupant;
        }

        // Only stat deltas are synchronized here. Defeated occupants and destruction effects
        // remain the shared resolver's responsibility after all direct damage has completed.
        public void RefreshOrcChieftainAuras()
        {
            if (_refreshingOrcChieftainAuras) return;
            var relevant = false;
            foreach (var occupant in EnumerateOccupants())
            {
                if (OrcChieftainRules.IsActiveSource(occupant)) { relevant = true; break; }
                foreach (var buff in occupant.CombatStatBuffs)
                    if (OrcChieftainRules.IsAura(buff)) { relevant = true; break; }
                if (relevant) break;
            }
            if (!relevant) return;
            _refreshingOrcChieftainAuras = true;
            try
            {
                var changed = true;
                while (changed)
                {
                    var occupants = new List<OccupantState>(EnumerateOccupants());
                    var sources = new List<OccupantState>();
                    foreach (var occupant in occupants)
                        if (OrcChieftainRules.IsActiveSource(occupant)) sources.Add(occupant);
                    changed = false;
                    foreach (var occupant in occupants)
                        changed |= occupant.ReconcileOrcChieftainAuras(sources);
                }
            }
            finally { _refreshingOrcChieftainAuras = false; }
        }

        public void Move(TileCoord from, TileCoord to)
        {
            if (!IsInside(from))
            {
                throw new ArgumentOutOfRangeException(nameof(from), "Source tile is outside the board.");
            }

            if (!IsInside(to))
            {
                throw new ArgumentOutOfRangeException(nameof(to), "Destination tile is outside the board.");
            }

            var occupant = _tiles[from.Column, from.Row];
            if (occupant == null)
            {
                throw new InvalidOperationException("대상 타일이 비어있습니다.");
            }

            if (_tiles[to.Column, to.Row] != null)
            {
                throw new InvalidOperationException("Cannot move onto an occupied tile.");
            }

            _tiles[from.Column, from.Row] = null;
            _tiles[to.Column, to.Row] = occupant;
            occupant.Position = to;
        }

        public void Swap(TileCoord first, TileCoord second)
        {
            if (!IsInside(first))
            {
                throw new ArgumentOutOfRangeException(nameof(first), "First tile is outside the board.");
            }

            if (!IsInside(second))
            {
                throw new ArgumentOutOfRangeException(nameof(second), "Second tile is outside the board.");
            }

            var firstOccupant = _tiles[first.Column, first.Row];
            var secondOccupant = _tiles[second.Column, second.Row];

            if (firstOccupant == null || secondOccupant == null)
            {
                throw new InvalidOperationException("Cannot swap unless both tiles are occupied.");
            }

            _tiles[first.Column, first.Row] = secondOccupant;
            _tiles[second.Column, second.Row] = firstOccupant;
            firstOccupant.Position = second;
            secondOccupant.Position = first;
        }

        public IEnumerable<OccupantState> EnumerateOccupants()
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                for (var row = 0; row < RowCount; row++)
                {
                    var occupant = _tiles[column, row];
                    if (occupant != null)
                    {
                        yield return occupant;
                    }
                }
            }
        }
    }
}
