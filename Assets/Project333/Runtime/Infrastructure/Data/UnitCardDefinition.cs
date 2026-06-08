using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Infrastructure.Data
{
    public sealed class UnitCardDefinition : CardDefinition
    {
        public UnitCardDefinition(
            string cardId,
            string displayName,
            ResourceSet cost,
            AttackType attackType,
            int attack,
            int health,
            bool canMove,
            bool isScience,
            int sciencePowerUpkeep,
            ResourceSet turnStartResourceGain = null,
            int maxAttacksPerTurn = 1,
            bool canAttackOnSummon = false,
            int hitsPerAttack = 1,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasGuard = false)
            : base(cardId, displayName, CardType.Unit, cost)
        {
            AttackType = attackType;
            Attack = attack;
            Health = health;
            CanMove = canMove;
            IsScience = isScience;
            SciencePowerUpkeep = sciencePowerUpkeep;
            TurnStartResourceGain = turnStartResourceGain?.Clone() ?? new ResourceSet();
            MaxAttacksPerTurn = maxAttacksPerTurn;
            CanAttackOnSummon = canAttackOnSummon;
            HitsPerAttack = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            HasBerserker = hasBerserker;
            HasEndure = hasEndure;
            HasGuard = hasGuard;
        }

        public AttackType AttackType { get; }
        public int Attack { get; }
        public int Health { get; }
        public bool CanMove { get; }
        public bool IsScience { get; }
        public int SciencePowerUpkeep { get; }
        public ResourceSet TurnStartResourceGain { get; }
        public int MaxAttacksPerTurn { get; }
        public bool CanAttackOnSummon { get; }
        public int HitsPerAttack { get; }
        public bool HasBerserker { get; }
        public bool HasEndure { get; }
        public bool HasGuard { get; }
    }
}
