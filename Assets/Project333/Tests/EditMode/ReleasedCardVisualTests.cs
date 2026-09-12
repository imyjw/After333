using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class ReleasedCardVisualTests
    {
        private const string Root = "Assets/Project333/ScriptableObjects/StarterTen";
        private static readonly string[] ReleasedIds =
        {
            "A-212", "A-301", "OrcWarrior", "Skeleton", "Werewolf", "Zombie",
            "BiochemicalBomb", "HuanShu", "TimedBomb", "GaebangBranch",
            "MerchantCaravan", "NuclearPowerPlant", "PowerPlant", "Gu",
            "TenThousandYearSnowGinseng", "PowerBank", "ManaStone"
        };
        private static readonly string[] OccupantIds =
        {
            "A-212", "A-301", "OrcWarrior", "Skeleton", "Werewolf", "Zombie",
            "GaebangBranch", "MerchantCaravan", "NuclearPowerPlant", "PowerPlant"
        };

        [TestCaseSource(nameof(ReleasedIds))]
        public void ReleasedCard_IsEnabledAndHasFullCardArtwork(string id)
        {
            var record = Database().Cards.Single(c => c.Id == id);
            Assert.That(record.IncludeInDraft, Is.True);
            var nonUpgradeable = new[] { "Gu", "HuanShu", "TenThousandYearSnowGinseng", "PowerBank", "ManaStone" };
            Assert.That(record.IncludeInRewards, Is.EqualTo(!nonUpgradeable.Contains(id)));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Project333/Resources/Project333/CardArtwork/" + id + ".png");
            Assert.That(sprite, Is.Not.Null, id);
            Assert.That(sprite.rect.width, Is.EqualTo(sprite.texture.width));
            Assert.That(sprite.rect.height, Is.EqualTo(sprite.texture.height));
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                Root + "/StarterTenCardCatalog.asset");
            Assert.That(catalog.TryGetCardAsset(id, out var card), Is.True);
            Assert.That(card.ToDefinition().CardId, Is.EqualTo(id));
            Assert.That(card.Cost.Mana, Is.EqualTo(record.Cost.Mana));
            Assert.That(card.Cost.Qi, Is.EqualTo(record.Cost.Qi));
            Assert.That(card.Cost.Power, Is.EqualTo(record.Cost.Power));
            Assert.That(card.Cost.Gold, Is.EqualTo(record.Cost.Gold));
        }

        [TestCaseSource(nameof(OccupantIds))]
        public void ReleasedOccupant_HasBoardSpriteAndPlayableAnimationStates(string id)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                Root + "/StarterTenCardCatalog.asset");
            Assert.That(catalog.TryGetCardAsset(id, out var card), Is.True);
            Assert.That(card.BoardSprite, Is.Not.Null);
            var controller = card.BoardAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            var actions = card is UnitCardDefinitionAsset
                ? new[] { "Idle", "Run", "Attack", "BeAttacked", "Death", "Drained" }
                : new[] { "Idle", "BeAttacked", "Death", "Drained" };
            foreach (var action in actions)
            {
                var state = states.SingleOrDefault(s => s.name == action);
                Assert.That(state, Is.Not.Null, id + ": " + action);
                var clip = state.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null, action);
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Single(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(frames.Length, Is.GreaterThan(1), action);
                Assert.That(frames.All(f => f.value is Sprite), Is.True, action);
                Assert.That(clip.length, Is.GreaterThan(0f), action);
            }
        }

        [Test]
        public void ReleasedRobots_IncludeA301AlongsideA111()
        {
            var robots = Database().ToDefinitions().OfType<UnitCardDefinition>()
                .Where(c => c.HasRobot && c.IncludeInDraft).Select(c => c.CardId);
            Assert.That(robots, Is.EquivalentTo(new[] { "A-111", "A-212", "A-301" }));
        }

        private static JsonCardDefinitionDatabase Database()
        {
            return JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
                "Assets/Project333/Resources/Project333/Data/cards.json"));
        }
    }
}
