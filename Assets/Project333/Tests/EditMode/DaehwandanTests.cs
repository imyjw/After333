using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.OwnedCards;

namespace Project333.Tests.EditMode
{
    public sealed class DaehwandanTests
    {
        private static int[] Levels => Enumerable.Range(0, 14).ToArray();
        private static ICardDefinitionProvider Provider() => JsonCardDefinitionDatabase.FromJson(
            File.ReadAllText("Assets/Project333/Resources/Project333/Data/cards.json")).CreateProvider();

        [TestCaseSource(nameof(Levels))]
        public void EveryLevel_BuffsOnlyOwnMasterAndNeverGeneratesQi(int level)
        {
            var provider = Provider();
            var definition = (ScriptedSpellCardDefinition)provider.GetRequired("Daehwandan");
            foreach (var owner in new[] { PlayerId.Player, PlayerId.AI })
            {
                var deck = Enumerable.Repeat("Goblin", 33).ToArray();
                var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, owner));
                state.SetPhase(PhaseType.Main);
                var caster = state.GetPlayer(owner);
                caster.Resources.Spend(caster.Resources.Clone());
                caster.Resources.Add(new ResourceSet(0, 5, 0, 0));
                caster.Hand.Add("Daehwandan");
                var levels = new InMemoryCardUpgradeLevelProvider();
                levels.SetUpgradeLevel(owner, "Daehwandan", level);
                new SpellService(provider, levels).CastScriptedSpell(state, owner, "Daehwandan");
                Assert.That(caster.Master.BaseAttack, Is.EqualTo(3 + 30 + level));
                Assert.That(state.GetPlayer(owner == PlayerId.Player ? PlayerId.AI : PlayerId.Player).Master.BaseAttack, Is.EqualTo(3));
                Assert.That(caster.Master.MaxHp, Is.EqualTo(333));
                Assert.That(caster.Resources.Qi, Is.Zero);
                Assert.That(state.PersistentEffects, Is.Empty);
                Assert.That(caster.Master.CombatStatBuffs.Single().AttackBonus, Is.EqualTo(30 + level));
                Assert.That(CardStatDisplay.GetEffectText(definition, DaehwandanCardRules.EffectDescription, level),
                    Is.EqualTo($"사용 시 내 마스터의 공격력을 {30 + level} 증가시킨다."));
                for (var turn = 0; turn < 3; turn++)
                {
                    state.RestoreRuntimeState(state.TurnNumber + 2, owner, PhaseType.TurnStart);
                    new TurnStartService(provider).ResolveTurnStart(state);
                    Assert.That(caster.Resources.Qi, Is.Zero);
                    Assert.That(caster.Master.BaseAttack, Is.EqualTo(33 + level));
                }
            }
        }

        [Test]
        public void BaseMagnitudeIsDataDrivenAndStacksUntilErasure()
        {
            var definition = new ScriptedSpellCardDefinition("Daehwandan", "대환단", new ResourceSet(), "daehwandan", effectAmount: 47);
            var provider = new InMemoryCardDefinitionProvider(new[] { definition });
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var state = new BattleSetupService().CreateInitialState(new BattleSetupRequest(deck, deck, PlayerId.Player));
            state.SetPhase(PhaseType.Main);
            var levels = new InMemoryCardUpgradeLevelProvider(); levels.SetUpgradeLevel(PlayerId.Player, "Daehwandan", 13);
            for (var i = 0; i < 2; i++)
            {
                state.Player.Hand.Add("Daehwandan");
                new SpellService(provider, levels).CastScriptedSpell(state, PlayerId.Player, "Daehwandan");
            }
            Assert.That(state.Player.Master.BaseAttack, Is.EqualTo(123));
            Assert.That(state.Player.Master.CombatStatBuffs.Single().AttackBonus, Is.EqualTo(120));
            state.Player.Master.ApplyErasure();
            Assert.That(state.Player.Master.BaseAttack, Is.EqualTo(3));
            Assert.That(state.PersistentEffects, Is.Empty);
        }

        [Test]
        public void NativeAndCanonicalDefinitionsAndUpgradeTableAgree()
        {
            var definition = (ScriptedSpellCardDefinition)Provider().GetRequired("Daehwandan");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/Cards/Daehwandan.asset");
            Assert.That(definition.EffectAmount, Is.EqualTo(30));
            Assert.That(asset.EffectAmount, Is.EqualTo(30));
            Assert.That(asset.EffectText, Is.EqualTo(DaehwandanCardRules.EffectDescription));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.True);
            Assert.That(CardUpgradeRules.IsCardUpgradeable(asset.ToDefinition()), Is.True);
            var table = OwnedCardsUpgradeTableData.Build(definition);
            Assert.That(table.EffectLabel, Is.EqualTo("마스터 공격력 증가량"));
            Assert.That(table.Rows.Count, Is.EqualTo(13));
            foreach (var row in table.Rows)
            {
                Assert.That(row.EffectIncrease, Is.EqualTo(1));
                Assert.That(row.AttackIncrease, Is.Zero);
                Assert.That(row.HpIncrease, Is.Zero);
            }
            Assert.That(CardStatDisplay.TryGetSpellDamage(definition, 13, 100, out _), Is.False);
        }
    }
}
