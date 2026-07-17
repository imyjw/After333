using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public sealed class MasterState : UnitState
    {
        public MasterState(
            string runtimeId,
            PlayerId ownerId,
            TileCoord position,
            int attack,
            int maxHp,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool hasFlying = false,
            int spellPower = 0)
            : base(
                runtimeId,
                "master",
                ownerId,
                position,
                AttackType.Melee,
                attack,
                maxHp,
                true,
                false,
                0,
                new ResourceSet(),
                1,
                OccupantKind.Master,
                damageType: DamageType.Physical,
                physicalDefense: physicalDefense,
                magicDefense: magicDefense,
                hasFlying: hasFlying,
                spellPower: spellPower)
        {
            HasSummoningSickness = false;
        }
    }
}
