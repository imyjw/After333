using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Domain.Cards
{
    public abstract class OccupantState
    {
        private bool _isDrained;
        private readonly List<InvincibleEffectState> _invincibleEffects = new List<InvincibleEffectState>();
        private PlayerId _currentActivePlayerId;
        private int _currentBattleTurnNumber;

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
            bool hasGuard = false,
            bool hasLifeSteal = false,
            DamageType damageType = DamageType.Physical,
            int physicalDefense = 0,
            int magicDefense = 0,
            bool hasRush = false,
            bool hasHiding = false,
            bool hasFlying = false,
            int spellPower = 0)
        {
            if (physicalDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(physicalDefense));
            }

            if (magicDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magicDefense));
            }

            if (spellPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spellPower));
            }

            if (hasHiding && kind != OccupantKind.Unit)
            {
                throw new ArgumentException("Only Units can have Hiding.", nameof(hasHiding));
            }

            RuntimeId = runtimeId;
            CardId = cardId;
            OwnerId = ownerId;
            _currentActivePlayerId = ownerId;
            Kind = kind;
            Position = position;
            AttackType = attackType;
            OriginalAttack = attack;
            BaseAttack = attack;
            OriginalMaxHp = maxHp;
            MaxHp = maxHp;
            CurrentHp = maxHp;
            CanMove = canMove;
            TurnStartResourceGain = turnStartResourceGain ?? new ResourceSet();
            MaxAttacksPerTurn = maxAttacksPerTurn;
            HitsPerAttack = hitsPerAttack < 1 ? 1 : hitsPerAttack;
            HasBerserker = hasBerserker;
            HasEndure = hasEndure;
            HasGuard = hasGuard;
            HasLifeSteal = hasLifeSteal;
            DamageType = damageType;
            OriginalPhysicalDefense = physicalDefense;
            OriginalMagicDefense = magicDefense;
            PhysicalDefense = physicalDefense;
            MagicDefense = magicDefense;
            HasRush = hasRush;
            HasHiding = hasHiding;
            HasFlying = hasFlying;
            SpellPower = spellPower;
            HasSummoningSickness = true;
            RemainingAttacksThisTurn = maxAttacksPerTurn;
        }

        public string RuntimeId { get; }
        public string CardId { get; }
        public PlayerId OwnerId { get; }
        public OccupantKind Kind { get; }
        public TileCoord Position { get; set; }
        public AttackType AttackType { get; protected set; }
        public int OriginalAttack { get; private set; }
        public int BaseAttack { get; protected set; }
        public int Attack => GetAttackForCurrentState();
        public int OriginalMaxHp { get; private set; }
        public int MaxHp { get; protected set; }
        public int CurrentHp { get; set; }
        public bool CanMove { get; protected set; }
        public ResourceSet TurnStartResourceGain { get; }
        public int MaxAttacksPerTurn { get; protected set; }
        public int HitsPerAttack { get; protected set; }
        public bool HasBerserker { get; protected set; }
        public bool HasEndure { get; protected set; }
        public bool HasGuard { get; protected set; }
        public bool HasLifeSteal { get; protected set; }
        public DamageType DamageType { get; protected set; }
        public int OriginalPhysicalDefense { get; private set; }
        public int OriginalMagicDefense { get; private set; }
        public int PhysicalDefense { get; protected set; }
        public int MagicDefense { get; protected set; }
        public bool HasRush { get; protected set; }
        public bool HasHiding { get; protected set; }
        public bool HidingRevealed { get; private set; }
        public bool IsHiding => HasHiding && !HidingRevealed && !IsSealbound;
        public bool HasFlying { get; protected set; }
        public bool HasActiveFlying => HasFlying && !EffectsSuppressed;
        public int SpellPower { get; protected set; }
        public int EffectiveSpellPower => IsAlive && !EffectsSuppressed ? SpellPower : 0;
        public IReadOnlyList<InvincibleEffectState> InvincibleEffects => _invincibleEffects;
        public bool IsInvincible
        {
            get
            {
                if (!IsAlive || EffectsSuppressed)
                {
                    return false;
                }

                foreach (var effect in _invincibleEffects)
                {
                    if (effect.IsActive(OwnerId, _currentActivePlayerId))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        public int EffectivePhysicalDefense => IsDrained ? 0 : PhysicalDefense;
        public int EffectiveMagicDefense => IsDrained ? 0 : MagicDefense;
        public bool HasActiveGuard => HasGuard && !EffectsSuppressed && !IsHiding && CurrentHp > 0;
        public int EffectiveMaxAttacksPerTurn => IsSealbound
            ? 0
            : EffectsSuppressed
            ? Math.Min(MaxAttacksPerTurn, 1)
            : MaxAttacksPerTurn;
        public int EffectiveHitsPerAttack => EffectsSuppressed ? 1 : HitsPerAttack;
        public bool EndureUsed { get; set; }
        public bool IsDrained
        {
            get => _isDrained;
            set
            {
                if (!IsErasure && !IsSealbound)
                {
                    _isDrained = value;
                    if (value)
                    {
                        RevealHiding();
                    }
                }
            }
        }
        public bool IsErasure { get; private set; }
        public bool IsSealbound { get; private set; }
        public int SealboundOwnerTurnStartsRemaining { get; private set; }
        public bool WasSummonedThisTurn { get; set; }
        public bool HasSummoningSickness { get; set; }
        public int RemainingAttacksThisTurn { get; set; }
        public bool IsAlive => CurrentHp > 0;

        public bool CannotAttack => IsDrained || IsSealbound;
        public bool CannotCounterattack => IsDrained || IsSealbound;
        public bool CannotMoveDueToState => IsDrained || IsSealbound;
        public bool EffectsSuppressed => IsDrained || IsErasure || IsSealbound;
        public bool BuffsAndDebuffsSuppressed => IsDrained || IsSealbound;
        public bool TakesDrainedDamage => IsDrained;
        public bool DoesNotBlockFrontRow => IsDrained || IsSealbound || IsHiding || HasActiveFlying;
        public bool CanBeAffected => !IsSealbound;

        public bool CanTriggerEndure => HasEndure && !EndureUsed && !EffectsSuppressed;

        public void SetBattleTurnContext(PlayerId activePlayerId, int turnNumber)
        {
            _currentActivePlayerId = activePlayerId;
            _currentBattleTurnNumber = Math.Max(0, turnNumber);
        }

        public InvincibleEffectState AddInvincibleEffect(
            InvincibleDurationType duration,
            int ownerTurns = 0,
            int appliedTurnNumber = -1,
            PlayerId? appliedActivePlayerId = null)
        {
            if (duration == InvincibleDurationType.None)
            {
                return null;
            }

            var resolvedTurnNumber = appliedTurnNumber < 0
                ? _currentBattleTurnNumber
                : appliedTurnNumber;
            var resolvedActivePlayerId = appliedActivePlayerId ?? _currentActivePlayerId;
            _currentBattleTurnNumber = Math.Max(0, resolvedTurnNumber);
            _currentActivePlayerId = resolvedActivePlayerId;
            var effect = new InvincibleEffectState(
                duration,
                duration == InvincibleDurationType.OwnerTurns ? ownerTurns : 0,
                resolvedTurnNumber,
                resolvedActivePlayerId);
            _invincibleEffects.Add(effect);
            return effect;
        }

        public void RestoreInvincibleEffects(IEnumerable<InvincibleEffectState> effects)
        {
            _invincibleEffects.Clear();
            if (effects == null)
            {
                return;
            }

            foreach (var effect in effects)
            {
                if (effect != null)
                {
                    _invincibleEffects.Add(effect.Clone());
                }
            }
        }

        public void ResolveInvincibleTurnEnd(PlayerId endingPlayerId)
        {
            for (var index = _invincibleEffects.Count - 1; index >= 0; index--)
            {
                if (_invincibleEffects[index].ResolveTurnEnd(OwnerId, endingPlayerId))
                {
                    _invincibleEffects.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Applies Erasure and removes combat-time modifiers that existed before it was applied.
        /// Account upgrade stats are part of the original summon stats and remain intact.
        /// </summary>
        public void ApplyErasure()
        {
            if (IsSealbound)
            {
                return;
            }

            var attacksSpent = Math.Max(0, MaxAttacksPerTurn - RemainingAttacksThisTurn);

            RevealHiding();
            IsErasure = true;
            _isDrained = false;
            BaseAttack = OriginalAttack;
            MaxHp = OriginalMaxHp;
            CurrentHp = Math.Min(CurrentHp, MaxHp);
            PhysicalDefense = OriginalPhysicalDefense;
            MagicDefense = OriginalMagicDefense;
            if (WasSummonedThisTurn)
            {
                HasSummoningSickness = true;
            }

            RemainingAttacksThisTurn = Math.Max(0, EffectiveMaxAttacksPerTurn - attacksSpent);
        }

        /// <summary>
        /// Restores serialized status flags without reapplying Erasure side effects.
        /// </summary>
        public void RestoreSuppressionStates(
            bool isDrained,
            bool isErasure,
            bool isSealbound = false,
            int sealboundOwnerTurnStartsRemaining = 0,
            bool hidingRevealed = false)
        {
            IsSealbound = Kind != OccupantKind.Master && isSealbound;
            SealboundOwnerTurnStartsRemaining = IsSealbound
                ? Math.Max(1, sealboundOwnerTurnStartsRemaining)
                : 0;
            IsErasure = !IsSealbound && isErasure;
            _isDrained = !IsErasure && isDrained;
            HidingRevealed = HasHiding &&
                             (hidingRevealed || IsErasure || (!IsSealbound && _isDrained));
        }

        public bool RevealHiding()
        {
            if (!HasHiding || HidingRevealed || IsSealbound)
            {
                return false;
            }

            HidingRevealed = true;
            return true;
        }

        public void EnterSealbound(int ownerTurnStarts)
        {
            if (ownerTurnStarts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerTurnStarts));
            }

            if (ownerTurnStarts == 0)
            {
                return;
            }

            if (Kind == OccupantKind.Master)
            {
                throw new InvalidOperationException("Master Units cannot enter Sealbound.");
            }

            IsSealbound = true;
            SealboundOwnerTurnStartsRemaining = ownerTurnStarts;
            HasSummoningSickness = true;
            RemainingAttacksThisTurn = 0;
        }

        public bool ResolveSealboundOwnerTurnStart()
        {
            if (!IsSealbound)
            {
                return false;
            }

            SealboundOwnerTurnStartsRemaining = Math.Max(0, SealboundOwnerTurnStartsRemaining - 1);
            if (SealboundOwnerTurnStartsRemaining > 0)
            {
                return false;
            }

            IsSealbound = false;
            var canAttackOnReleaseTurn = HasRush && !EffectsSuppressed;
            HasSummoningSickness = !canAttackOnReleaseTurn;
            RemainingAttacksThisTurn = canAttackOnReleaseTurn ? EffectiveMaxAttacksPerTurn : 0;
            return true;
        }

        /// <summary>
        /// Restores the account-upgraded summon baseline used when future Erasure is applied.
        /// </summary>
        public void RestoreOriginalCombatStats(
            int originalAttack,
            int originalMaxHp,
            int originalPhysicalDefense,
            int originalMagicDefense)
        {
            if (originalPhysicalDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originalPhysicalDefense));
            }

            if (originalMagicDefense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originalMagicDefense));
            }

            OriginalAttack = originalAttack;
            OriginalMaxHp = originalMaxHp;
            OriginalPhysicalDefense = originalPhysicalDefense;
            OriginalMagicDefense = originalMagicDefense;
        }

        public void IncreaseBaseAttack(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (!IsSealbound)
            {
                BaseAttack += amount;
            }
        }

        public void IncreaseMaxHpAndCurrentHp(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (!IsSealbound)
            {
                MaxHp += amount;
                CurrentHp += amount;
            }
        }

        public void IncreasePhysicalDefense(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (!IsSealbound)
            {
                PhysicalDefense += amount;
            }
        }

        public void IncreaseMagicDefense(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (!IsSealbound)
            {
                MagicDefense += amount;
            }
        }

        public void Heal(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount == 0 || IsSealbound)
            {
                return;
            }

            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        }

        private int GetAttackForCurrentState()
        {
            if (!HasBerserker || EffectsSuppressed)
            {
                return BaseAttack;
            }

            var lostHp = Math.Max(0, MaxHp - CurrentHp);
            return BaseAttack + lostHp;
        }
    }
}
