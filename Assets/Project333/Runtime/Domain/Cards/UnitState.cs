using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public class UnitState : OccupantState
    {
        public UnitState(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            TileCoord position,
            AttackType attackType,
            int attack,
            int maxHp,
            bool canMove,
            bool isScience,
            int sciencePowerUpkeep,
            ResourceSet turnStartResourceGain = null,
            int maxAttacksPerTurn = 1,
            OccupantKind kind = OccupantKind.Unit,
            int hitsPerAttack = 1,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasGuard = false)
            : base(
                runtimeId,
                cardId,
                ownerId,
                kind,
                position,
                attackType,
                attack,
                maxHp,
                canMove,
                turnStartResourceGain ?? new ResourceSet(),
                maxAttacksPerTurn,
                hitsPerAttack,
                hasBerserker,
                hasEndure,
                hasGuard)
        {
            IsScience = isScience;
            SciencePowerUpkeep = sciencePowerUpkeep;
        }

        public bool IsScience { get; }
        public int SciencePowerUpkeep { get; }
    }
}
