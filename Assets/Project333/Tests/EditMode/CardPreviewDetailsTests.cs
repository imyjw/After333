using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using Project333.Runtime.Presentation.Draft;
using Project333.Runtime.Presentation.Hand;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class CardPreviewDetailsTests
    {
        private GameObject _owner;

        [SetUp] public void Setup() => _owner = new GameObject("PreviewOwner");
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_owner);
            foreach (var overlay in Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>())
                Object.DestroyImmediate(overlay.gameObject);
        }

        private static UnitCardDefinition Unit() => new UnitCardDefinition("BlueDragon", "블루드래곤",
            new ResourceSet(), AttackType.Ranged, 10, 20, true, false, 0,
            maxAttacksPerTurn: 2, hitsPerAttack: 3, hasEndure: true, hasRush: true,
            hasHiding: true, hasFlying: true, spellPower: 10, hasPiercing: true);

        private static DeckCardPreviewOverlay Overlay() => Resources.FindObjectsOfTypeAll<DeckCardPreviewOverlay>().Single();
        private static string[] VisibleTitles() => Overlay().GetComponentsInChildren<Text>()
            .Where(t => t.name == "Title").Select(t => t.text).ToArray();
        private static void Refresh() => typeof(DeckCardPreviewOverlay).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Overlay(), null);

        [Test]
        public void CardOnlyPreview_ShowsInnateEffectsAndNoBattleStatuses()
        {
            DeckCardPreviewOverlay.ShowFor(_owner.transform, "BlueDragon", null, null, definition: Unit());
            Assert.That(VisibleTitles(), Does.Contain("3연타").And.Contain("속공").And.Contain("관통").And.Contain("추가 공격 1").And.Contain("불굴"));
            Assert.That(Overlay().GetComponentsInChildren<Text>().Where(t => t.name == "Description").All(t => !string.IsNullOrEmpty(t.text)), Is.True);
            Assert.That(Overlay().transform.Cast<Transform>().Any(t => t.gameObject.activeSelf && t.name.StartsWith("Attached")), Is.False);
            Assert.That(Overlay().transform.Cast<Transform>().Any(t => t.gameObject.activeSelf && t.name.StartsWith("Influence")), Is.False);
            DeckCardPreviewOverlay.HideFor(_owner.transform);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(_owner.transform), Is.False);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void PowerConsumptionPreview_ShowsBothRulesInHandAndCardOnlyContexts(bool building, bool handContext)
        {
            CardDefinition definition = building
                ? (CardDefinition)new BuildingCardDefinition("RobotFactory", "로봇 공장", new ResourceSet(),
                    false, 0, 20, sciencePowerUpkeep: 1)
                : new UnitCardDefinition("A-212", "A-212", new ResourceSet(),
                    AttackType.Melee, 3, 20, true, true, 2);
            var hand = handContext ? _owner.AddComponent<HandCardView>() : null;
            if (hand != null) hand.Present(definition.CardId, "held-power", false);
            DeckCardPreviewOverlay.ShowFor(handContext ? (Component)hand : _owner.transform,
                definition.CardId, null, null, definition: definition, handCard: hand);

            Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { $"전력소모: {(building ? 1 : 2)}", "방전" }));
            var abilityPanels = Overlay().transform.Cast<Transform>()
                .Where(panel => panel.gameObject.activeSelf && panel.name.StartsWith("AbilityTooltip")).ToArray();
            Assert.That(abilityPanels.Length, Is.EqualTo(2));
            Assert.That(abilityPanels.All(panel => panel.Find("Description").gameObject.activeSelf), Is.True);
            Assert.That(Overlay().transform.Cast<Transform>()
                .Any(panel => panel.gameObject.activeSelf && panel.name.StartsWith("AttachedTooltip")), Is.False);

            DeckCardPreviewOverlay.ShowFor(_owner.transform, "plain", null, null,
                definition: new UnitCardDefinition("plain", "유닛", new ResourceSet(),
                    AttackType.Melee, 1, 10, true, false, 0));
            Assert.That(VisibleTitles(), Is.Empty, "Reusing a card preview must remove both related rules.");
        }
        [Test]
        public void HandPreview_RefreshesSpellPowerAndTemporaryCopy_ThenClearsOnOtherCard()
        {
            var hand = _owner.AddComponent<HandCardView>();
            hand.Present("Fireball", "held", true, 10);
            var spell = new DamageSpellCardDefinition("Fireball", "파이어볼", new ResourceSet(), 20, hasReplicate: true);
            DeckCardPreviewOverlay.ShowFor(hand, spell.CardId, null, null, definition: spell, handCard: hand);
            Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { "복제", "임시 복제", "주문력 +10" }));
            var firstAttack = int.Parse(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text);
            hand.Present("Fireball", "held", false, 20);
            Refresh();
            Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { "복제", "주문력 +20" }));
            Assert.That(int.Parse(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text), Is.EqualTo(firstAttack + 10));
            hand.Present("Fireball", "held", false, 0);
            Refresh();
            Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { "복제" }));
            hand.Present("Fireball", "replacement", false);
            Refresh();
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(hand), Is.False);

            DeckCardPreviewOverlay.ShowFor(_owner.transform, "unknown", null, null);
            Assert.That(VisibleTitles(), Is.Empty, "Reusing the overlay must not leak another card's panels.");
        }

        [Test]
        public void NuclearHandPreview_ShowsOnlyFalloutWhileUpgradeBadgeRefreshes()
        {
            var levels = (System.Collections.Generic.IDictionary<string, int>)typeof(Project333.Runtime.Application.Accounts.AccountSessionState)
                .GetField("OwnedCardUpgradeLevels", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var hadLevel = levels.TryGetValue(NuclearRules.CardId, out var previousLevel);
            try
            {
                var record = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text)
                    .Cards.Single(card => card.Id == NuclearRules.CardId);
                Assert.That(OccupantSpecialEffectTooltipCatalog.Build(record.ToDefinition()).Select(entry => entry.Title),
                    Is.EquivalentTo(new[] { "낙진" }));
                var hand = _owner.AddComponent<HandCardView>();
                hand.Present(NuclearRules.CardId, "nuclear-preview", false, spellPower: 999);
                levels[NuclearRules.CardId] = 0;
                DeckCardPreviewOverlay.ShowFor(hand, NuclearRules.CardId, null, null, definition: record.ToDefinition(), handCard: hand);
                foreach (var level in new[] { 0, 1, 13 })
                {
                    levels[NuclearRules.CardId] = level;
                    Refresh();
                    Assert.That(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text, Is.EqualTo((100 + level).ToString()));
                    Assert.That(VisibleTitles(), Does.Contain("낙진").And.Not.Contain("뉴클리어"));
                    var panels = Overlay().transform.Cast<Transform>()
                        .Where(panel => panel.gameObject.activeSelf && panel.name.StartsWith("AbilityTooltip")).ToArray();
                    Assert.That(panels.Length, Is.EqualTo(1));
                    var description = panels.Single().Find("Description").GetComponent<Text>().text;
                    Assert.That(description, Does.Contain("양쪽 턴 시작마다 중첩당 10").And.Contain("영구").And.Contain("중첩"));
                    Assert.That(description, Does.Not.Contain("고정 데미지 " + (100 + level)));
                    Assert.That(Overlay().transform.Find("CardArtwork/Hp").gameObject.activeSelf, Is.False);
                }
            }
            finally
            {
                if (hadLevel) levels[NuclearRules.CardId] = previousLevel;
                else levels.Remove(NuclearRules.CardId);
            }
        }

        [Test]
        public void TutorialPreview_UsesLocalLevelForStatsAndTooltipsThenRestoresAccountLevel()
        {
            var levels = (System.Collections.Generic.IDictionary<string, int>)typeof(Project333.Runtime.Application.Accounts.AccountSessionState)
                .GetField("OwnedCardUpgradeLevels", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var hadLevel = levels.TryGetValue(NuclearRules.CardId, out var previousLevel);
            try
            {
                levels[NuclearRules.CardId] = 13;
                var definition = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text)
                    .Cards.Single(card => card.Id == NuclearRules.CardId).ToDefinition();
                DeckCardPreviewOverlay.ShowFor(_owner.transform, NuclearRules.CardId, null, null,
                    definition: definition, upgradeLevelOverride: 0);
                Refresh();
                Assert.That(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text, Is.EqualTo("100"));
                Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { "낙진" }));
                var tutorialDescription = Overlay().GetComponentsInChildren<Text>()
                    .Single(t => t.name == "Description").text;
                Assert.That(tutorialDescription, Does.Contain("중첩당 10").And.Not.Contain("고정 데미지 100"));
                Assert.That(levels[NuclearRules.CardId], Is.EqualTo(13));

                // A subsequent ordinary preview must not inherit the tutorial's local level.
                DeckCardPreviewOverlay.ShowFor(_owner.transform, NuclearRules.CardId, null, null, definition: definition);
                Assert.That(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text, Is.EqualTo("113"));
                Assert.That(VisibleTitles(), Is.EquivalentTo(new[] { "낙진" }));
                Assert.That(Overlay().GetComponentsInChildren<Text>().Single(t => t.name == "Description").text,
                    Is.EqualTo(tutorialDescription));
            }
            finally
            {
                if (hadLevel) levels[NuclearRules.CardId] = previousLevel;
                else levels.Remove(NuclearRules.CardId);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MeteorPreview_UpgradeAndHandSpellPowerRefreshTheEffectAndBadge(bool useHand)
        {
            var levels = (System.Collections.Generic.IDictionary<string, int>)typeof(Project333.Runtime.Application.Accounts.AccountSessionState)
                .GetField("OwnedCardUpgradeLevels", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var hadLevel = levels.TryGetValue(MeteorRules.CardId, out var previousLevel);
            try
            {
                levels[MeteorRules.CardId] = 0;
                var definition = JsonCardDefinitionDatabase.FromJson(Resources.Load<TextAsset>("Project333/Data/cards").text)
                    .Cards.Single(card => card.Id == MeteorRules.CardId).ToDefinition();
                var hand = useHand ? _owner.AddComponent<HandCardView>() : null;
                if (hand != null)
                {
                    hand.Present(MeteorRules.CardId, "meteor-preview", false, spellPower: 0);
                    DeckCardPreviewOverlay.ShowFor(hand, MeteorRules.CardId, null, null, definition: definition, handCard: hand);
                }
                else DeckCardPreviewOverlay.ShowFor(_owner.transform, MeteorRules.CardId, null, null, definition: definition);
                foreach (var level in new[] { 0, 1, 13 })
                foreach (var spellPower in useHand ? new[] { 0, 10, 20 } : new[] { 0 })
                {
                    levels[MeteorRules.CardId] = level;
                    if (hand != null) hand.Present(MeteorRules.CardId, "meteor-preview", false, spellPower: spellPower);
                    Refresh();
                    Assert.That(VisibleTitles(), Does.Contain("메테오"));
                    var description = Overlay().GetComponentsInChildren<Text>()
                        .Where(text => text.name == "Description").Select(text => text.text).Single(text => text.Contains("마법 데미지"));
                    Assert.That(description, Does.Contain("마법 데미지 " + (40 + level) + "을 입힌다."));
                    Assert.That(Overlay().transform.Find("CardArtwork/Attack").GetComponent<Text>().text,
                        Is.EqualTo((40 + level + spellPower).ToString()));
                    Assert.That(Overlay().transform.Find("CardArtwork/Hp").gameObject.activeSelf, Is.False);
                    if (spellPower > 0) Assert.That(VisibleTitles(), Does.Contain("주문력 +" + spellPower));
                    else Assert.That(VisibleTitles().Any(title => title.StartsWith("주문력 +")), Is.False);
                }
            }
            finally
            {
                if (hadLevel) levels[MeteorRules.CardId] = previousLevel;
                else levels.Remove(MeteorRules.CardId);
            }
        }

        [Test]
        public void DefinitionProjection_SupportsBuildingsAndSpellReplicate()
        {
            var building = new BuildingCardDefinition("PowerPlant", "발전소", new ResourceSet(), false, 0, 20);
            Assert.That(OccupantSpecialEffectTooltipCatalog.Build(building).Select(t => t.Title), Does.Contain("액티브"));
            var spell = new DamageSpellCardDefinition("spell", "Spell", new ResourceSet(), 10, hasReplicate: true);
            Assert.That(OccupantSpecialEffectTooltipCatalog.Build(spell).Select(t => t.Title), Is.EquivalentTo(new[] { "복제" }));
            Assert.That(OccupantSpecialEffectTooltipCatalog.Build((CardDefinition)null), Is.Empty);
        }

        [TestCase(1920,1080)]
        [TestCase(2340,1080)]
        [TestCase(1280,720)]
        public void PreviewPanelsFitAndRemainOutsideCard(int width, int height)
        {
            var hand = _owner.AddComponent<HandCardView>();
            hand.Present("BlueDragon", "preview", true, 20);
            DeckCardPreviewOverlay.ShowFor(hand, "BlueDragon", null, null, definition: Unit(), handCard: hand);
            var overlay = Overlay();
            overlay.GetComponent<CanvasScaler>().enabled = false;
            overlay.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var root = (RectTransform)overlay.transform;
            root.sizeDelta = new Vector2(width, height);
            Canvas.ForceUpdateCanvases();
            Refresh();
            var card = (RectTransform)root.Find("CardArtwork");
            var cardRect = new Rect(card.anchoredPosition - card.sizeDelta * .5f, card.sizeDelta);
            var panels = root.Cast<Transform>().Where(t => t.gameObject.activeSelf && t.name.Contains("Tooltip")).Cast<RectTransform>().ToArray();
            Assert.That(panels.Length, Is.GreaterThan(3));
            foreach(var panel in panels)
            {
                var bounds = new Rect(panel.anchoredPosition-panel.sizeDelta*.5f,panel.sizeDelta);
                Assert.That(bounds.Overlaps(cardRect), Is.False);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-width*.5f));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(width*.5f));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-height*.5f));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(height*.5f));
                if(panel.name.StartsWith("Attached")) Assert.That(bounds.yMax, Is.LessThanOrEqualTo(cardRect.yMin));
                if(panel.name.StartsWith("Influence")) Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(cardRect.yMax));
            }
            if(width == 1920) Capture(overlay);
        }

        [Test]
        public void HandInfluences_SeparateSourcesAndExcludeBoardHazards()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var battle = new Project333.Runtime.Application.Services.BattleSetupService().CreateInitialState(
                new Project333.Runtime.Application.Services.BattleSetupRequest(deck, deck, Project333.Runtime.Domain.Battle.PlayerId.Player));
            var owner = Project333.Runtime.Domain.Battle.PlayerId.Player;
            var unit = new UnitState("source", "ManaPond", owner, new Project333.Runtime.Domain.Board.TileCoord(0,0),
                AttackType.Melee, 0, 10, true, false, 0, spellPower: 20);
            battle.GetBoard(owner).Place(unit.Position, unit);
            var amplification = new Project333.Runtime.Domain.Effects.PersistentEffectState("amplification", owner,
                MagicAmplificationCircleRules.EffectId, 0, string.Empty, new ResourceSet());
            battle.PersistentEffects.Add(amplification);
            battle.PersistentEffects.Add(new Project333.Runtime.Domain.Effects.PersistentEffectState("firewall",
                Project333.Runtime.Domain.Battle.PlayerId.AI, "firewall", 0, string.Empty, new ResourceSet(), targetRow: 0, remainingTriggers: 2, effectDamage: 10));
            var entries = OccupantStatusTooltipCatalog.BuildHandInfluences(battle, owner);
            Assert.That(entries.Select(e => e.Title), Is.EquivalentTo(new[] { "주문력 +20", "마법 증폭진: 주문력 +10" }));
            Assert.That(entries.All(e => string.IsNullOrEmpty(e.Description)), Is.True);
            var hand = _owner.AddComponent<HandCardView>();
            hand.Present("BlueDragon", "held", false, 30);
            DeckCardPreviewOverlay.ShowFor(hand, "BlueDragon", null, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                definition: Unit(), handCard: hand, battleProvider: () => battle);
            Assert.That(VisibleTitles(), Does.Contain("마법 증폭진: 주문력 +10").And.Contain("주문력 +20"));
            Assert.That(Overlay().GetComponentsInChildren<Text>().First(t => t.name == "Title").font.HasCharacter('가'), Is.True);
            battle.PersistentEffects.Remove(amplification);
            hand.Present("BlueDragon", "held", false, 20);
            Refresh();
            Assert.That(VisibleTitles(), Does.Not.Contain("마법 증폭진: 주문력 +10"));
        }


        private static void Capture(DeckCardPreviewOverlay overlay)
        {
            var cameraObject = new GameObject("PreviewCamera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            var rt=new RenderTexture(1920,1080,24);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.1f,.14f,.18f);
                camera.transform.position=new Vector3(0,0,-10); camera.orthographic=true;
                var canvas=overlay.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases(); Refresh(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=rt;
                var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
                try {image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();var dir=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../TempBuild/CardPreviewDetails"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"preview.png"),image.EncodeToPNG());}
                finally {Object.DestroyImmediate(image);}
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);}
        }
    }
}
