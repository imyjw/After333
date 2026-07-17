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
            bool hasGuard = false,
            bool hasLifeSteal = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool hasRobot = false,
            bool hasRush = false,
            bool hasHiding = false,
            bool hasFlying = false,
            int spellPower = 0)
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
                hasGuard,
                hasLifeSteal,
                damageType,
                physicalDefense,
                magicDefense,
                hasRush,
                hasHiding,
                hasFlying,
                spellPower)
        {
            IsScience = isScience;
            SciencePowerUpkeep = sciencePowerUpkeep;
            HasRobot = hasRobot;
        }

        public bool IsScience { get; }
        public int SciencePowerUpkeep { get; }
        public bool HasRobot { get; }
        public bool HasActiveRobot => HasRobot && !IsErasure && !IsSealbound;
    }
}
