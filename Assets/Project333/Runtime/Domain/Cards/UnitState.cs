using System.Collections.Generic;
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
            bool hasShielder = false,
            bool hasLifeSteal = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            IReadOnlyList<UnitRace> races = null,
            bool hasRush = false,
            bool hasHiding = false,
            bool hasFlying = false,
            int spellPower = 0,
            bool hasPiercing = false)
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
                hasShielder,
                hasLifeSteal,
                damageType,
                physicalDefense,
                magicDefense,
                hasRush,
                hasHiding,
                hasFlying,
                spellPower,
                hasPiercing)
        {
            IsScience = isScience;
            SciencePowerUpkeep = sciencePowerUpkeep;
            Races = UnitRaceCollection.Copy(races);
        }

        public bool IsScience { get; }
        public int SciencePowerUpkeep { get; }
        public IReadOnlyList<UnitRace> Races { get; }
        public bool HasRace(UnitRace race) => UnitRaceCollection.Contains(Races, race);
    }
}
