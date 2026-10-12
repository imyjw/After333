using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Cards;
using Project333.Runtime.Presentation.OwnedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class MirrorTrapRegistrationTests
    {
        private const string JsonPath = "Assets/Project333/Resources/Project333/Data/cards.json";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/TrapMirrorField.asset";
        private const string ArtPath = "Assets/Project333/Resources/Project333/CardArtwork/TrapMirrorField.png";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        private static JsonCardDefinitionRecord MirrorRecord(JsonCardDefinitionDatabase database) =>
            database.Cards.Single(card => card.Id == TrapMirrorFieldCardRules.CardId);

        [UnityTearDown]
        public IEnumerator ExitAfterFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void CanonicalMirrorIsFreeFantasyRareReflectionWithDraftOnlyEligibility()
        {
            var database = Database();
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.True, string.Join(Environment.NewLine, validation.Errors));
            var record = MirrorRecord(database);
            Assert.That(record.DisplayName, Is.EqualTo(TrapMirrorFieldCardRules.DisplayName));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.TrapSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.Fantasy));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.False);
            Assert.That(record.EffectText, Is.Empty);
            Assert.That(record.SpecialEffectText, Is.EqualTo(TrapMirrorFieldCardRules.SpecialEffectText));
            Assert.That(record.Damage, Is.Zero, "Reflection uses the triggering attacker's current attack, not card damage.");
            Assert.That(record.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(record.EffectAmount, Is.Zero);
            var trap = record.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(trap, Is.Not.Null);
            Assert.That(trap.CardType, Is.EqualTo(CardType.Spell));
            Assert.That(trap.HasTrap, Is.True);
            Assert.That(trap.HasReplicate, Is.False);
            Assert.That(new[] { trap.Cost.Mana, trap.Cost.Qi, trap.Cost.Power, trap.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(new[] { trap.ActivationCost.Mana, trap.ActivationCost.Qi,
                trap.ActivationCost.Power, trap.ActivationCost.Gold }, Is.EqualTo(new[] { 2, 0, 0, 0 }));
            Assert.That(trap.TriggerType, Is.EqualTo(TrapTriggerType.EnemyUnitAttacksUnit));
            Assert.That(trap.EffectType, Is.EqualTo(TrapEffectType.ReflectAttackerAttackAsMagic));
        }

        [TestCase("activation")]
        [TestCase("setup")]
        [TestCase("returnTrigger")]
        [TestCase("returnEffect")]
        [TestCase("rewards")]
        public void MirrorDefinitionCannotSilentlyBecomePaidSetupReturnTrapOrRewardCard(string mutation)
        {
            var database = Database();
            var record = MirrorRecord(database);
            switch (mutation)
            {
                case "activation": record.TrapActivationCost.Mana = 1; break;
                case "setup": record.Cost.Mana = 1; break;
                case "returnTrigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksMaster; break;
                case "returnEffect": record.TrapEffect = TrapEffectType.ReturnAttackerToHand; break;
                case "rewards": record.IncludeInRewards = true; break;
                default: throw new InvalidOperationException("Unknown mirror metadata mutation.");
            }
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, string.Join(Environment.NewLine, validation.Errors));
        }

        [Test]
        public void NativeMirrorMatchesCanonicalCostsTraitAndMetadataWithoutSummonedVisuals()
        {
            var record = MirrorRecord(Database());
            var asset = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.CardId, Is.EqualTo(record.Id));
            Assert.That(asset.DisplayName, Is.EqualTo(record.DisplayName));
            Assert.That(asset.Rarity, Is.EqualTo(record.Rarity));
            Assert.That(asset.Affiliation, Is.EqualTo(record.Affiliation));
            Assert.That(asset.ChargeTileFootprint, Is.EqualTo(record.ChargeTileFootprint));
            Assert.That(asset.EffectText, Is.EqualTo(record.EffectText));
            Assert.That(asset.SpecialEffectText, Is.EqualTo(record.SpecialEffectText));
            Assert.That(asset.HasTrap, Is.True);
            Assert.That(asset.HasReplicate, Is.False);
            Assert.That(asset.BoardSprite, Is.Null);
            Assert.That(asset.BoardAnimatorController, Is.Null);
            var definition = asset.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(definition, Is.Not.Null);
            Assert.That(new[] { definition.Cost.Mana, definition.Cost.Qi, definition.Cost.Power, definition.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(new[] { definition.ActivationCost.Mana, definition.ActivationCost.Qi,
                definition.ActivationCost.Power, definition.ActivationCost.Gold }, Is.EqualTo(new[] { 2, 0, 0, 0 }));
            Assert.That(definition.TriggerType, Is.EqualTo(record.TrapTrigger));
            Assert.That(definition.EffectType, Is.EqualTo(record.TrapEffect));
        }

        [Test]
        public void MirrorAppendsOnceAfterThePreservedReturnCardWithoutReplacingCatalogReferences()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cards.Count, Is.EqualTo(53));
            Assert.That(catalog.Cards.All(card => card != null), Is.True);
            Assert.That(catalog.Cards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(53));
            Assert.That(catalog.Cards.Count(card => card.HasTrap), Is.EqualTo(5));
            Assert.That(catalog.Cards[49].CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(catalog.Cards[48].CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(catalog.Cards.Single(card => card.CardId == TrapMirrorFieldCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath)));
            Assert.That(catalog.Cards.Single(card => card.CardId == TrapReturnCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(
                    "Assets/Project333/ScriptableObjects/StarterTen/TrapReturn.asset")));
        }

        [Test]
        public void MirrorArtworkIsTheCompleteNativePortraitWithStableResourceIdentity()
        {
            var sprite = Resources.Load<Sprite>("Project333/CardArtwork/" + TrapMirrorFieldCardRules.CardId);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(ArtPath));
            Assert.That(sprite.name, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(sprite.vertices.Length, Is.EqualTo(4), "FullRect retains all beam/mirror artwork.");
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(ArtPath), false), Is.True);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, source.width, source.height)));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(source.width / 2f, source.height / 2f)));
                Assert.That(source.GetPixel(0, 0).a, Is.Zero, "Generated portrait keeps real alpha rather than a black bitmap backdrop.");
            }
            finally { Object.DestroyImmediate(source); }
            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(ArtPath)));
            Assert.That(fileId, Is.Not.Zero);
            CardArtworkLibrary.ClearCacheForTests();
            Assert.That(CardArtworkLibrary.TryGetArtwork(TrapMirrorFieldCardRules.CardId, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(sprite), "Card artwork uses the imported sprite, not a cropped/fallback runtime sprite.");
            Assert.That(sprite, Is.Not.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden")));
            Assert.That(sprite, Is.Not.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapReturn")));
        }

        [Test]
        public void MirrorKeepsTheSharedSecretTooltipAndNoUpgradeRows()
        {
            foreach (var definition in new CardDefinition[] { MirrorRecord(Database()).ToDefinition(),
                AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath).ToDefinition() })
            {
                Assert.That(CardUpgradeRules.IsCardUpgradeable(definition), Is.False);
                var table = OwnedCardsUpgradeTableData.Build(definition);
                Assert.That(table.IsUpgradeable, Is.False);
                Assert.That(table.Rows, Is.Empty);
                var tooltip = OccupantSpecialEffectTooltipCatalog.Build(definition).Single(entry => entry.Title == "트랩");
                Assert.That(tooltip.Description, Does.Contain("무료로 설치").And.Contain("발동 비용")
                    .And.Contain("한 번").And.Contain("비용이 부족").And.Contain("상대에게 종류가 공개되지")
                    .And.Contain("같은 트랩"));
            }
        }

        [TestCase(BattleEventType.TrapInstalled, PlayerId.Player, false)]
        [TestCase(BattleEventType.TrapInstalled, PlayerId.AI, true)]
        [TestCase(BattleEventType.TrapTriggered, PlayerId.Player, true)]
        [TestCase(BattleEventType.TrapTriggered, PlayerId.AI, true)]
        public void MirrorInstallStaysAnonymousWhileEitherSeatCanSeeTheTriggeredCard(BattleEventType kind,
            PlayerId owner, bool visible)
        {
            var entry = new BattleEventDto { EventType = kind, SourceOwnerId = owner, TrapCount = 1,
                CardId = kind == BattleEventType.TrapTriggered ? TrapMirrorFieldCardRules.CardId : string.Empty };
            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(entry), Is.EqualTo(visible));
            Assert.That(OpponentPlayedCardRevealRules.IsBoardCardPlay(entry), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualPreviewRejectsHiddenIdentityAndRevealsNativeMirrorAfterActivation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new PreviewFixture())
            {
                var hidden = Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden");
                var mirror = Resources.Load<Sprite>("Project333/CardArtwork/" + TrapMirrorFieldCardRules.CardId);
                Assert.That(hidden, Is.Not.Null); Assert.That(mirror, Is.Not.Null);
                fixture.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapInstalled, SourceOwnerId = PlayerId.AI,
                        CardId = TrapMirrorFieldCardRules.CardId, SourceCardId = TrapMirrorFieldCardRules.CardId,
                        CardSpellDamage = 777, CardAttack = 777, CardMaxHp = 777, TrapCount = 1 }
                });
                Assert.That(Field<Image>(fixture.Bootstrapper, "_opponentPlayedCardRevealImage").sprite, Is.SameAs(hidden));
                Assert.That(Field<Text>(fixture.Bootstrapper, "_opponentPlayedCardTrapTitleText").text, Is.EqualTo("트랩"));
                Assert.That(Field<Text>(fixture.Bootstrapper, "_opponentPlayedCardFallbackText").text, Is.EqualTo("?"));
                Assert.That(Field<Text>(fixture.Bootstrapper, "_opponentPlayedCardAttackText").gameObject.activeSelf, Is.False);
                Assert.That(Field<Text>(fixture.Bootstrapper, "_opponentPlayedCardHpText").gameObject.activeSelf, Is.False);
                fixture.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapTriggered, SourceOwnerId = PlayerId.Player,
                        CardId = TrapMirrorFieldCardRules.CardId, TrapCount = 0 }
                });
                var deadline = Time.realtimeSinceStartup + 2f;
                while (Field<Image>(fixture.Bootstrapper, "_opponentPlayedCardRevealImage").sprite != mirror
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Field<Image>(fixture.Bootstrapper, "_opponentPlayedCardRevealImage").sprite, Is.SameAs(mirror));
                Assert.That(Field<Text>(fixture.Bootstrapper, "_opponentPlayedCardTrapTitleText").gameObject.activeSelf, Is.False);
                var group = Field<CanvasGroup>(fixture.Bootstrapper, "_opponentPlayedCardRevealCanvasGroup");
                Assert.That(group.blocksRaycasts, Is.False); Assert.That(group.interactable, Is.False);
                fixture.Bootstrapper.ClearOpponentPlayedCardReveals();
                Assert.That(group.gameObject.activeSelf, Is.False);
            }
        }

        private sealed class PreviewFixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly BattleBootstrapper Bootstrapper;
            private readonly string _previousSession;
            internal PreviewFixture()
            {
                _previousSession = AccountSessionState.SessionToken;
                SetSession("mirror-trap-preview-no-requests");
                Root = new GameObject("IsolatedMirrorTrapPreview"); Root.SetActive(false);
                Bootstrapper = Root.AddComponent<BattleBootstrapper>(); Bootstrapper.enabled = false;
                Set(Bootstrapper, "_startOnAwake", false);
                Set(Bootstrapper, "_startWithDraftBeforeBattle", false);
                Set(Bootstrapper, "_autoRunAiTurns", false);
                Set(Bootstrapper, "_opponentPlayedCardNormalDisplaySeconds", .18f);
                Set(Bootstrapper, "_opponentPlayedCardQueuedDisplaySeconds", .12f);
                Set(Bootstrapper, "_opponentPlayedCardFadeInSeconds", .02f);
                Set(Bootstrapper, "_opponentPlayedCardFadeOutSeconds", .02f);
                Root.SetActive(true);
                Set(Bootstrapper, "_cardDefinitionProvider", new InMemoryCardDefinitionProvider(
                    new CardDefinition[] { TrapMirrorFieldCardRules.CreateDefinition(), TrapReturnCardRules.CreateDefinition() }));
                foreach (var name in new[] { "_opponentPlayedCardRevealCanvasGroup", "_yourTurnBannerCanvasGroup" })
                    if (typeof(BattleBootstrapper).GetField(name, Hidden)?.GetValue(Bootstrapper) is Component component)
                        component.transform.SetParent(Root.transform, false);
            }
            public void Dispose() { Object.DestroyImmediate(Root); SetSession(_previousSession); }
        }

        private static void SetSession(string value) => typeof(AccountSessionState)
            .GetField("<SessionToken>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
    }
}
