using System;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public abstract class OccupantState
    {
        protected OccupantState(
            string runtimeId,
            string cardId,
            PlayerId ownerId,
            OccupantKind kind,
            TileCoord position,
            AttackType attackType,
            int attack,
            int maxHp,
            bool canMove,
            ResourceSet turnStartResourceGain,
            int maxAttacksPerTurn,
            int hitsPerAttack,
            bool hasBerserker = false,
            bool hasEndure = false,
            bool hasGuard = false)
        {
            RuntimeId = runtimeId;
            CardId = cardId;
            OwnerId = ownerId;
            Kind = kind;
            Position = position;
            AttackType = attackType;
            BaseAttack = attack;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            CanMove = canMove;
            TurnStartResourceGain = turnStartResourceGain ?? new ResourceSet();
            MaxAttacksPerTurn = maxAttacksPerTurn;
            HitsPerAttack = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            HasBerserker = hasBerserker;
            HasEndure = hasEndure;
            HasGuard = hasGuard;
            HasSummoningSickness = true;
            RemainingAttacksThisTurn = maxAttacksPerTurn;
        }

        public string RuntimeId { get; }
        public string CardId { get; }
        public PlayerId OwnerId { get; }
        public OccupantKind Kind { get; }
        public TileCoord Position { get; set; }
        public AttackType AttackType { get; protected set; }
        public int BaseAttack { get; protected set; }
        public int Attack => GetAttackForCurrentState();
        public int MaxHp { get; protected set; }
        public int CurrentHp { get; set; }
        public bool CanMove { get; protected set; }
        public ResourceSet TurnStartResourceGain { get; }
        public int MaxAttacksPerTurn { get; protected set; }
        public int HitsPerAttack { get; protected set; }
        public bool HasBerserker { get; protected set; }
        public bool HasEndure { get; protected set; }
        public bool HasGuard { get; protected set; }
        public bool HasActiveGuard => HasGuard && !IsDisabled && CurrentHp > 0;
        public bool EndureUsed { get; set; }
        public bool IsDisabled { get; set; }
        public bool HasSummoningSickness { get; set; }
        public int RemainingAttacksThisTurn { get; set; }
        public bool IsAlive => CurrentHp > 0;

        public bool CanTriggerEndure => HasEndure && !EndureUsed && !IsDisabled;

        public void IncreaseBaseAttack(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            BaseAttack += amount;
        }

        public void Heal(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount == 0)
            {
                return;
            }

            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        }

        private int GetAttackForCurrentState()
        {
            if (!HasBerserker || IsDisabled)
            {
                return BaseAttack;
            }

            var lostHp = Math.Max(0, MaxHp - CurrentHp);
            return BaseAttack + lostHp;
        }
    }
}
