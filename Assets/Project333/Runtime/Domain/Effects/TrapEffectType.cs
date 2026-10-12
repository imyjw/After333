namespace Project333.Runtime.Domain.Effects
{
    public enum TrapEffectType
    {
        None = 0,
        ReturnAttackerToHand = 1,
        ReflectAttackerAttackAsMagic = 2,
        DestroyAttacker = 3,
        DamageSummonedUnitPhysical = 4,
    }
}
