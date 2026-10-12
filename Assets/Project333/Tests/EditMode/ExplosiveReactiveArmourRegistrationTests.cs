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
    public sealed class ExplosiveReactiveArmourRegistrationTests
    {
        private const string JsonPath = "Assets/Project333/Resources/Project333/Data/cards.json";
        private const string CardPath = "Assets/Project333/ScriptableObjects/StarterTen/ExplosiveReactiveArmour.asset";
        private const string ArtPath = "Assets/Project333/Resources/Project333/CardArtwork/ExplosiveReactiveArmour.png";
        private const string CatalogPath = "Assets/Project333/ScriptableObjects/StarterTen/StarterTenCardCatalog.asset";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static JsonCardDefinitionDatabase Database() => JsonCardDefinitionDatabase.FromJson(File.ReadAllText(JsonPath));
        private static JsonCardDefinitionRecord ArmourRecord(JsonCardDefinitionDatabase database) =>
            database.Cards.Single(card => card.Id == ExplosiveReactiveArmourCardRules.CardId);

        [UnityTearDown]
        public IEnumerator ExitAfterFailure()
        {
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void CanonicalArmourIsFreeScienceRareDirectDestructionWithDraftOnlyEligibility()
        {
            var database = Database();
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.True, string.Join(Environment.NewLine, validation.Errors));
            Assert.That(database.Cards.Count, Is.EqualTo(60));
            Assert.That(database.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(60));
            Assert.That(database.Cards.Count(card => card.DefinitionType == JsonCardDefinitionKind.TrapSpell), Is.EqualTo(5));
            var record = ArmourRecord(database);
            Assert.That(record.DisplayName, Is.EqualTo(ExplosiveReactiveArmourCardRules.DisplayName));
            Assert.That(record.DefinitionType, Is.EqualTo(JsonCardDefinitionKind.TrapSpell));
            Assert.That(record.Rarity, Is.EqualTo(CardRarity.Rare));
            Assert.That(record.Affiliation, Is.EqualTo(CardAffiliation.ScienceCivilization));
            Assert.That(record.ChargeTileFootprint, Is.EqualTo(ChargeTileFootprint.None));
            Assert.That(record.IncludeInDraft, Is.True);
            Assert.That(record.IncludeInRewards, Is.False);
            Assert.That(record.EffectText, Is.Empty);
            Assert.That(record.SpecialEffectText, Is.EqualTo(ExplosiveReactiveArmourCardRules.SpecialEffectText));
            Assert.That(record.Damage, Is.Zero, "Direct destruction is not a card damage effect.");
            Assert.That(record.DamageType, Is.EqualTo(DamageType.None));
            Assert.That(record.EffectAmount, Is.Zero);
            Assert.That(record.TriggerCount, Is.Zero);
            Assert.That(record.HasTrap, Is.True);
            var trap = record.ToDefinition() as TrapSpellCardDefinition;
            Assert.That(trap, Is.Not.Null);
            Assert.That(trap.CardType, Is.EqualTo(CardType.Spell));
            Assert.That(trap.HasTrap, Is.True);
            Assert.That(trap.HasReplicate, Is.False);
            Assert.That(new[] { trap.Cost.Mana, trap.Cost.Qi, trap.Cost.Power, trap.Cost.Gold },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(new[] { trap.ActivationCost.Mana, trap.ActivationCost.Qi,
                trap.ActivationCost.Power, trap.ActivationCost.Gold }, Is.EqualTo(new[] { 0, 0, 4, 0 }));
            Assert.That(trap.TriggerType, Is.EqualTo(TrapTriggerType.EnemyMeleeUnitAttacksMaster));
            Assert.That(trap.EffectType, Is.EqualTo(TrapEffectType.DestroyAttacker));
        }

        [TestCase("activation")]
        [TestCase("setup")]
        [TestCase("unitTrigger")]
        [TestCase("oldAnyUnitTrigger")]
        [TestCase("returnEffect")]
        [TestCase("rewards")]
        [TestCase("draft")]
        [TestCase("damage")]
        public void ArmourDefinitionRejectsPaidSetupWrongActivationOrDamageAndRewardDrift(string mutation)
        {
            var database = Database();
            var record = ArmourRecord(database);
            switch (mutation)
            {
                case "activation": record.TrapActivationCost.Power = 5; break;
                case "setup": record.Cost.Gold = 1; break;
                case "unitTrigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksUnit; break;
                case "oldAnyUnitTrigger": record.TrapTrigger = TrapTriggerType.EnemyUnitAttacksMaster; break;
                case "returnEffect": record.TrapEffect = TrapEffectType.ReturnAttackerToHand; break;
                case "rewards": record.IncludeInRewards = true; break;
                case "draft": record.IncludeInDraft = false; break;
                case "damage": record.Damage = 10; record.DamageType = DamageType.Magic; break;
                default: throw new InvalidOperationException("Unknown reactive armour metadata mutation.");
            }
            var validation = CardDatabaseValidator.Validate(database);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, string.Join(Environment.NewLine, validation.Errors));
        }

        [Test]
        public void NativeArmourMatchesCanonicalCostsTraitAndMetadataWithoutSummonedVisuals()
        {
            var record = ArmourRecord(Database());
            var asset = AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(CardPath));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string cardGuid, out long cardFileId), Is.True);
            Assert.That(cardGuid, Is.Not.Empty);
            Assert.That(AssetDatabase.GUIDToAssetPath(cardGuid), Is.EqualTo(CardPath));
            Assert.That(cardFileId, Is.Not.Zero);
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
                definition.ActivationCost.Power, definition.ActivationCost.Gold }, Is.EqualTo(new[] { 0, 0, 4, 0 }));
            Assert.That(definition.TriggerType, Is.EqualTo(record.TrapTrigger));
            Assert.That(definition.EffectType, Is.EqualTo(record.TrapEffect));
        }

        [Test]
        public void ArmourAppendsOnceAfterPreservedReturnAndMirrorNativeReferences()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Cards.Count, Is.EqualTo(53));
            Assert.That(catalog.Cards.All(card => card != null), Is.True);
            Assert.That(catalog.Cards.Select(card => card.CardId).Distinct().Count(), Is.EqualTo(53));
            Assert.That(catalog.Cards.Count(card => card.HasTrap), Is.EqualTo(5));
            Assert.That(catalog.Cards[50].CardId, Is.EqualTo(ExplosiveReactiveArmourCardRules.CardId));
            Assert.That(catalog.Cards[49].CardId, Is.EqualTo(TrapMirrorFieldCardRules.CardId));
            Assert.That(catalog.Cards[48].CardId, Is.EqualTo(TrapReturnCardRules.CardId));
            Assert.That(catalog.Cards.Single(card => card.CardId == ExplosiveReactiveArmourCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(CardPath)));
            Assert.That(catalog.Cards.Single(card => card.CardId == TrapMirrorFieldCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(
                    "Assets/Project333/ScriptableObjects/StarterTen/TrapMirrorField.asset")));
            Assert.That(catalog.Cards.Single(card => card.CardId == TrapReturnCardRules.CardId),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TrapSpellCardDefinitionAsset>(
                    "Assets/Project333/ScriptableObjects/StarterTen/TrapReturn.asset")));
        }

        [Test]
        public void ArmourArtworkIsTheCompleteNativeSquareWithStableResourceIdentity()
        {
            var sprite = Resources.Load<Sprite>("Project333/CardArtwork/" + ExplosiveReactiveArmourCardRules.CardId);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(ArtPath));
            Assert.That(sprite.name, Is.EqualTo(ExplosiveReactiveArmourCardRules.CardId));
            Assert.That(sprite.vertices.Length, Is.EqualTo(4), "FullRect retains the complete barrier, projectile and reactive explosion.");
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(ArtPath), false), Is.True);
                Assert.That(new Vector2Int(source.width, source.height), Is.EqualTo(new Vector2Int(1254, 1254)));
                Assert.That(new Vector2Int(sprite.texture.width, sprite.texture.height),
                    Is.EqualTo(new Vector2Int(1254, 1254)), "The native import must not resize this non-power-of-two image.");
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, source.width, source.height)));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(source.width / 2f, source.height / 2f)));
                foreach (var corner in new[] { new Vector2Int(0, 0), new Vector2Int(1253, 0),
                    new Vector2Int(0, 1253), new Vector2Int(1253, 1253) })
                    Assert.That(source.GetPixel(corner.x, corner.y).a, Is.LessThanOrEqualTo(2f / 255f),
                        "Generated artwork retains real transparency; a one-byte alpha fringe is preserved.");
            }
            finally { Object.DestroyImmediate(source); }
            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(1254));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.Not.Empty);
            Assert.That(guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(ArtPath)));
            Assert.That(fileId, Is.Not.Zero);
            CardArtworkLibrary.ClearCacheForTests();
            Assert.That(CardArtworkLibrary.TryGetArtwork(ExplosiveReactiveArmourCardRules.CardId, out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(sprite), "Card artwork uses the imported sprite, not a cropped/fallback runtime sprite.");
            Assert.That(sprite, Is.Not.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden")));
            Assert.That(sprite, Is.Not.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapReturn")));
            Assert.That(sprite, Is.Not.SameAs(Resources.Load<Sprite>("Project333/CardArtwork/TrapMirrorField")));
        }

        [Test]
        public void ArmourKeepsTheSharedSecretTooltipAndNoUpgradeRows()
        {
            foreach (var definition in new CardDefinition[] { ArmourRecord(Database()).ToDefinition(),
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
        public void ArmourInstallStaysAnonymousWhileEitherSeatCanSeeTheTriggeredCard(BattleEventType kind,
            PlayerId owner, bool visible)
        {
            var entry = new BattleEventDto { EventType = kind, SourceOwnerId = owner, TrapCount = 1,
                CardId = kind == BattleEventType.TrapTriggered ? ExplosiveReactiveArmourCardRules.CardId : string.Empty };
            Assert.That(OpponentPlayedCardRevealRules.ShouldReveal(entry), Is.EqualTo(visible));
            Assert.That(OpponentPlayedCardRevealRules.IsBoardCardPlay(entry), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualPreviewRejectsHiddenIdentityAndRevealsNativeArmourAfterActivation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            using (var fixture = new PreviewFixture())
            {
                var hidden = Resources.Load<Sprite>("Project333/CardArtwork/TrapHidden");
                var armour = Resources.Load<Sprite>("Project333/CardArtwork/" + ExplosiveReactiveArmourCardRules.CardId);
                Assert.That(hidden, Is.Not.Null); Assert.That(armour, Is.Not.Null);
                fixture.Bootstrapper.QueueOpponentPlayedCardReveals(new[]
                {
                    new BattleEventDto { EventType = BattleEventType.TrapInstalled, SourceOwnerId = PlayerId.AI,
                        CardId = ExplosiveReactiveArmourCardRules.CardId, SourceCardId = ExplosiveReactiveArmourCardRules.CardId,
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
                        CardId = ExplosiveReactiveArmourCardRules.CardId, TrapCount = 0 }
                });
                var deadline = Time.realtimeSinceStartup + 2f;
                while (Field<Image>(fixture.Bootstrapper, "_opponentPlayedCardRevealImage").sprite != armour
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Field<Image>(fixture.Bootstrapper, "_opponentPlayedCardRevealImage").sprite, Is.SameAs(armour));
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
                SetSession("reactive-armour-preview-no-requests");
                Root = new GameObject("IsolatedReactiveArmourPreview"); Root.SetActive(false);
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
                    new CardDefinition[] { ExplosiveReactiveArmourCardRules.CreateDefinition(),
                        TrapMirrorFieldCardRules.CreateDefinition(), TrapReturnCardRules.CreateDefinition() }));
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
