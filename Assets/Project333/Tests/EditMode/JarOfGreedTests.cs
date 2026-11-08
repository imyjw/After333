using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Tests.EditMode
{
    public sealed class JarOfGreedTests
    {
        private static JsonCardDefinitionDatabase Database()
        {
#if UNITY_EDITOR
            var path = Path.Combine(UnityEngine.Application.dataPath, "Project333/Resources/Project333/Data/cards.json");
#else
            var path = "Assets/Project333/Resources/Project333/Data/cards.json";
#endif
            return JsonCardDefinitionDatabase.FromJson(File.ReadAllText(path));
        }

        [Test]
        public void ActualDefinitionIsNeutralRareOneGoldDraftIncludedRewardExcludedAndNotUpgradeable()
        {
            var database = Database();
            Assert.That(CardDatabaseValidator.Validate(database).Errors, Is.Empty);
            var record = database.Cards.Single(card => card.Id == JarOfGreedRules.CardId);
            Assert.That(record.DisplayName, Is.EqualTo("욕망의 단지"));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.False);
            Assert.That(record.Cost.Gold, Is.EqualTo(1));
            Assert.That(record.EffectAmount, Is.EqualTo(2));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(record.ToDefinition()), Is.False);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void CastConsumesOneGoldAndSelectedCardAndDrawsTwoForCasterOnly(PlayerId actor)
        {
            var state = State(actor);
            var caster = state.GetPlayer(actor);
            var other = state.GetOpponent(actor);
            var selected = caster.Hand.Add(JarOfGreedRules.CardId);
            var sibling = caster.Hand.Add(JarOfGreedRules.CardId);
            Cast(state, actor, selected.RuntimeId);
            Assert.That(caster.Resources.Gold, Is.Zero);
            Assert.That(caster.Hand.Contains(JarOfGreedRules.CardId, selected.RuntimeId), Is.False);
            Assert.That(caster.Hand.Contains(JarOfGreedRules.CardId, sibling.RuntimeId), Is.True);
            Assert.That(caster.Hand.CardIds, Does.Contain("Zombie").And.Contain("Skeleton"));
            Assert.That(caster.Deck.Count, Is.Zero);
            Assert.That(caster.Discard.CardIds, Is.EqualTo(new[] { JarOfGreedRules.CardId }));
            Assert.That(other.Hand.Count, Is.Zero);
            Assert.That(other.Deck.Count, Is.EqualTo(2));
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(2));
            Assert.That(state.CardDrawEvents.All(e => e.OwnerId == actor && e.SourceCardId == JarOfGreedRules.CardId), Is.True);
        }

        [Test]
        public void InsufficientGoldDoesNotConsumeOrDraw()
        {
            var state = State();
            state.Player.Resources.Spend(new ResourceSet(0, 0, 0, 1));
            var hand = state.Player.Hand.Add(JarOfGreedRules.CardId);
            Assert.Throws<InvalidOperationException>(() => Cast(state, PlayerId.Player, hand.RuntimeId));
            Assert.That(state.Player.Hand.Contains(JarOfGreedRules.CardId), Is.True);
            Assert.That(state.Player.Deck.Count, Is.EqualTo(2));
            Assert.That(state.Player.Discard.Count, Is.Zero);
            Assert.That(state.CardDrawEvents, Is.Empty);
        }

        [Test]
        public void MissingOrStaleHandIdentityDoesNotSpendOrDraw()
        {
            var state = State();
            var hand = state.Player.Hand.Add(JarOfGreedRules.CardId);
            Assert.Throws<InvalidOperationException>(() => Cast(state, PlayerId.Player, "stale"));
            Assert.That(state.Player.Hand.Contains(JarOfGreedRules.CardId, hand.RuntimeId), Is.True);
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(state.Player.Deck.Count, Is.EqualTo(2));
        }

        [Test]
        public void WrongTurnOrBoardTargetDoesNotSpend()
        {
            var state = State();
            state.Player.Hand.Add(JarOfGreedRules.CardId);
            var service = new SpellService(Database().CreateProvider());
            Assert.Throws<InvalidOperationException>(() => service.CastScriptedSpell(state, PlayerId.AI, JarOfGreedRules.CardId));
            Assert.Throws<InvalidOperationException>(() => service.CastScriptedSpell(state, PlayerId.Player,
                JarOfGreedRules.CardId, PlayerId.AI, new TileCoord(2, 1)));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(1));
            Assert.That(state.Player.Hand.Count, Is.EqualTo(1));
            Assert.That(state.Player.Deck.Count, Is.EqualTo(2));
        }

        [Test]
        public void FullHandBurnsOverflowAfterSpellFreesItsOwnSlot()
        {
            var state = State();
            var selected = state.Player.Hand.Add(JarOfGreedRules.CardId);
            for (var i = 0; i < 9; i++) state.Player.Hand.Add("Zombie");
            Cast(state, PlayerId.Player, selected.RuntimeId);
            Assert.That(state.Player.Hand.Count, Is.EqualTo(10));
            Assert.That(state.Player.Deck.Count, Is.Zero);
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(1));
            Assert.That(state.Player.FailedDrawCount, Is.Zero);
        }

        [TestCase(0, 9, 2)]
        [TestCase(1, 3, 1)]
        public void EmptyDeckUsesExistingFatigueForEachFailedDraw(int available, int damage, int failures)
        {
            var state = State();
            while (state.Player.Deck.Count > available) state.Player.Deck.TryDraw(out _);
            var selected = state.Player.Hand.Add(JarOfGreedRules.CardId);
            var hp = state.Player.Master.CurrentHp;
            Cast(state, PlayerId.Player, selected.RuntimeId);
            Assert.That(state.Player.FailedDrawCount, Is.EqualTo(failures));
            Assert.That(state.Player.Master.CurrentHp, Is.EqualTo(hp - damage));
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(available));
        }

        [Test]
        public void LethalFatigueEndsBattleAndStopsRemainingDraw()
        {
            var state = State();
            while (state.Player.Deck.TryDraw(out _)) { }
            state.Player.Master.CurrentHp = 3;
            var selected = state.Player.Hand.Add(JarOfGreedRules.CardId);
            Cast(state, PlayerId.Player, selected.RuntimeId);
            Assert.That(state.IsEnded, Is.True);
            Assert.That(state.Player.FailedDrawCount, Is.EqualTo(1));
            Assert.That(state.Player.Master.CurrentHp, Is.Zero);
        }

        [Test]
        public void ReplayedSelectedCardCommandCannotChargeOrDrawAgain()
        {
            var state = State(); state.Player.Resources.Add(new ResourceSet(0, 0, 0, 2));
            var selected = state.Player.Hand.Add(JarOfGreedRules.CardId);
            Cast(state, PlayerId.Player, selected.RuntimeId);
            Assert.Throws<InvalidOperationException>(() => Cast(state, PlayerId.Player, selected.RuntimeId));
            Assert.That(state.Player.Resources.Gold, Is.EqualTo(2));
            Assert.That(state.CardDrawEvents.Count, Is.EqualTo(2));
        }

        [Test]
        public void UntargetedClassificationIncludesNewSpellsWithoutAnAnimationAllowlist()
        {
            var future = new ScriptedSpellCardDefinition("FutureSpell", "Future", new ResourceSet(), "future_non_targeted_effect");
            Assert.That(SpellCardUseRules.IsUntargeted(future), Is.True);
            Assert.That(SpellCardUseRules.IsUntargeted(Database().CreateProvider().GetRequired(JarOfGreedRules.CardId)), Is.True);
            foreach (var effect in new[] { "cheonra_jimang", "firewall", "fire_spear", "plasma_shield", "mana_shield", "biochemical_bomb", "gu", "huan_shu", "robot_fusion" })
                Assert.That(SpellCardUseRules.IsUntargeted(new ScriptedSpellCardDefinition(effect, effect, new ResourceSet(), effect)), Is.False, effect);
        }

        [TestCase("cost")]
        [TestCase("draft")]
        [TestCase("rewards")]
        [TestCase("count")]
        [TestCase("identity")]
        public void ValidatorRejectsUnapprovedJarMetadata(string change)
        {
            var db = Database(); var card = db.Cards.Single(c => c.Id == JarOfGreedRules.CardId);
            if (change == "cost") card.Cost.Gold = 2;
            if (change == "draft") card.IncludeInDraft = false;
            if (change == "rewards") card.IncludeInRewards = true;
            if (change == "count") card.EffectAmount = 3;
            if (change == "identity") card.Id = "NotJarOfGreed";
            Assert.That(CardDatabaseValidator.Validate(db).Errors, Has.Some.Contains("JarOfGreed must be"));
        }

#if UNITY_EDITOR
        [Test]
        public void NativeBuilderIsIdempotentAndDefinitionMatchesJson()
        {
            Project333.Editor.Project333JarOfGreedCardBuilder.Build();
            const string assetPath = "Assets/Project333/ScriptableObjects/StarterTen/Cards/JarOfGreed.asset";
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            Project333.Editor.Project333JarOfGreedCardBuilder.Build();
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(guid));
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptedSpellCardDefinitionAsset>(assetPath);
            var definition = (ScriptedSpellCardDefinition)asset.ToDefinition();
            Assert.That(asset.DisplayName, Is.EqualTo(JarOfGreedRules.DisplayName));
            Assert.That(asset.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(asset.Affiliation, Is.EqualTo(CardAffiliation.Neutral));
            var artwork = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(
                "Assets/Project333/Resources/Project333/CardArtwork/JarOfGreed.png");
            Assert.That(artwork, Is.Not.Null);
            Assert.That(artwork.rect.width, Is.EqualTo(artwork.texture.width));
            Assert.That(definition.Cost.Gold, Is.EqualTo(1));
            Assert.That(definition.EffectAmount, Is.EqualTo(2));
            Assert.That(definition.EffectId, Is.EqualTo(JarOfGreedRules.EffectId));
            Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset");
            Assert.That(catalog.Cards.Count(c => c.CardId == JarOfGreedRules.CardId), Is.EqualTo(1));
        }
#endif

        private static void Cast(BattleState state, PlayerId actor, string runtimeId) =>
            new SpellService(Database().CreateProvider()).CastScriptedSpell(state, actor, JarOfGreedRules.CardId, runtimeId);

        private static BattleState State(PlayerId actor = PlayerId.Player)
        {
            var p = Player(PlayerId.Player); var a = Player(PlayerId.AI);
            var pb = new BoardState(); var ab = new BoardState();
            pb.Place(p.Master.Position, p.Master); ab.Place(a.Master.Position, a.Master);
            var state = new BattleState(p, a, pb, ab);
            state.RestoreRuntimeState(5, actor, PhaseType.Main);
            return state;
        }

        private static PlayerState Player(PlayerId owner) => new PlayerState(owner, new ResourceSet(0, 0, 0, 1),
            new DeckState(new[] { "Skeleton", "Zombie" }), new HandState(), new DiscardState(),
            new MasterState(owner.ToString(), owner, new TileCoord(2, 1), 3, 333));
    }
}
