namespace Project333.Runtime.Domain.Effects
{
    public enum TrapTriggerType
    {
        None = 0,
        EnemyUnitAttacksMaster = 1,
        // Both attacker and protected target may be ordinary Units or Masters.
        EnemyUnitAttacksUnit = 2,
        // Ordinary melee Units attacking the trap owner's actual Master.
        EnemyMeleeUnitAttacksMaster = 3,
        // Ordinary ranged Units attacking the trap owner's actual Master.
        EnemyRangedUnitAttacksMaster = 4,
        // A successfully initialized ordinary enemy Unit played from hand.
        EnemyUnitSummoned = 5,
    }
}
