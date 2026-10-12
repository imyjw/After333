using System.Collections.Generic;
using System.Linq;
using Project333.Runtime.Application.Gateways;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Effects;
using Project333.Runtime.Domain.Resources;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Presentation.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class TileCardPreviewTests
    {
        private GameObject _canvasObject;
        private TileView _tile;
        private TileTextView _view;
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _canvasObject = new GameObject("PreviewTestCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            var tileObject = new GameObject("Tile", typeof(RectTransform));
            tileObject.transform.SetParent(_canvasObject.transform, false);
            tileObject.SetActive(false);
            _tile = tileObject.AddComponent<TileView>();
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(tileObject.transform, false);
            var imageObject = new GameObject("Fallback", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(tileObject.transform, false);
            _texture = new Texture2D(3, 4);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 3f, 4f), new Vector2(0.5f, 0.5f));
            var image = imageObject.GetComponent<Image>();
            image.sprite = _sprite;
            _view = tileObject.AddComponent<TileTextView>();
            SetField("_tileView", _tile);
            SetField("_text", labelObject.GetComponent<TMP_Text>());
            SetField("_occupantVisualImage", image);
            tileObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObject);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        [TestCase(PlayerId.Player)]
        [TestCase(PlayerId.AI)]
        public void Show_UsesRuntimeAttackAndRemainingHpForEitherPlayer(PlayerId owner)
        {
            var unit = CreateUnit("unit", owner, 21, 54);
            unit.IncreaseBaseAttack(5);
            unit.CurrentHp = 17;
            _tile.Present(unit);

            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            Assert.That(Stat("AttackValueText").text, Is.EqualTo("26"));
            Assert.That(Stat("HpValueText").text, Is.EqualTo("17"));
            Assert.That(Stat("AttackValueText").raycastTarget, Is.False);
            Assert.That(Stat("HpValueText").gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void Show_NonAttackingBuildingDisplaysZeroAttackAndCurrentHp()
        {
            var building = new BuildingState("building", "PreviewTestBuilding", PlayerId.AI,
                new TileCoord(0, 0), false, 0, 45);
            building.CurrentHp = 29;
            _tile.Present(building);

            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            Assert.That(Stat("AttackValueText").text, Is.EqualTo("0"));
            Assert.That(Stat("HpValueText").text, Is.EqualTo("29"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Show_PowerConsumptionExplainsDrainedAlongsideTheCard_AndRefreshesOnlyTheCurrentStatusBelow(bool building)
        {
            OccupantState occupant = building
                ? (OccupantState)new BuildingState("power-preview", "PreviewPowerBuilding", PlayerId.Player,
                    new TileCoord(0, 0), false, 0, 20, sciencePowerUpkeep: 3)
                : new UnitState("power-preview", "PreviewPowerUnit", PlayerId.Player,
                    new TileCoord(0, 0), AttackType.Melee, 3, 20, true, false, 3);
            _tile.Present(occupant);

            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            foreach (var drained in new[] { false, true, false })
            {
                occupant.IsDrained = drained;
                RefreshPreview();
                var abilities = VisiblePanels("SpecialEffectTooltip");
                Assert.That(abilities.Select(panel => panel.Find("Title").GetComponent<TMP_Text>().text),
                    Is.EquivalentTo(new[] { "전력소모: 3", "방전" }));
                Assert.That(abilities.All(panel => panel.Find("Description").gameObject.activeSelf), Is.True);
                var attached = VisiblePanels("AttachedEffectTooltip");
                Assert.That(attached.Length, Is.EqualTo(drained ? 1 : 0));
                if (drained)
                {
                    Assert.That(attached[0].Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("방전"));
                    Assert.That(attached[0].Find("Description").gameObject.activeSelf, Is.False);
                }
                Assert.That(occupant.IsDrained, Is.EqualTo(drained));
            }
        }
        [Test]
        public void Refresh_UpdatesBuffsDamageAndReplacementStateWithSameRuntimeId()
        {
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            var replacement = CreateUnit("unit", PlayerId.Player, 35, 60);
            replacement.CurrentHp = 11;
            _tile.Present(replacement);
            RefreshPreview();

            Assert.That(Stat("AttackValueText").text, Is.EqualTo("35"));
            Assert.That(Stat("HpValueText").text, Is.EqualTo("11"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Refresh_ClosesWhenOccupantLeavesOrIsReplaced(bool replace)
        {
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            _tile.Present(replace ? CreateUnit("different", PlayerId.Player, 30, 50) : null);
            RefreshPreview();

            Assert.That(Overlay().gameObject.activeSelf, Is.False);
            Assert.That(Stat("AttackValueText").text, Is.Empty);
            Assert.That(Stat("HpValueText").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Hide_UnrelatedTileCannotCloseSharedPreview()
        {
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            var other = new GameObject("OtherTile", typeof(RectTransform), typeof(TileTextView));
            other.transform.SetParent(_canvasObject.transform, false);
            other.GetComponent<TileTextView>().HideCurrentCardPreview();

            Assert.That(Overlay().gameObject.activeSelf, Is.True);
            _view.HideCurrentCardPreview();
            Assert.That(Overlay().gameObject.activeSelf, Is.False);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1280f, 720f)]
        [TestCase(800f, 450f)]
        [TestCase(720f, 1280f)]
        public void Refresh_AnchorsNumbersToRenderedCardAfterResize(float width, float height)
        {
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            _canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            Canvas.ForceUpdateCanvases();
            RefreshPreview();
            var image = Overlay().Find("PreviewCardImage").GetComponent<Image>();
            var container = image.rectTransform.rect;
            var drawnHeight = Mathf.Min(container.height, container.width / 0.75f);
            var drawnWidth = drawnHeight * 0.75f;
            AssertPosition("AttackValueText", new Vector2(-drawnWidth * 0.41f, -drawnHeight * 0.445f));
            AssertPosition("HpValueText", new Vector2(drawnWidth * 0.42f, -drawnHeight * 0.445f));
            Assert.That(Stat("AttackValueText").fontSize,
                Is.EqualTo(Mathf.RoundToInt(44f * drawnHeight / 620f)));
        }

        [Test]
        public void Refresh_PreservesInspectorFontSizeAndCustomPositions()
        {
            SetField("_cardPreviewStatFontSize", 55);
            SetField("_cardPreviewAttackNormalizedPosition", new Vector2(0.1f, 0.06f));
            SetField("_cardPreviewHpNormalizedPosition", new Vector2(0.9f, 0.08f));
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);

            _canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1280f, 720f);
            Canvas.ForceUpdateCanvases();
            RefreshPreview();

            var image = Overlay().Find("PreviewCardImage").GetComponent<Image>();
            var container = image.rectTransform.rect;
            var drawnHeight = Mathf.Min(container.height, container.width / 0.75f);
            var drawnWidth = drawnHeight * 0.75f;
            AssertPosition("AttackValueText", new Vector2(-drawnWidth * 0.4f, -drawnHeight * 0.44f));
            AssertPosition("HpValueText", new Vector2(drawnWidth * 0.4f, -drawnHeight * 0.42f));
            Assert.That(Stat("AttackValueText").fontSize,
                Is.EqualTo(Mathf.RoundToInt(55f * drawnHeight / 620f)));
        }

        [Test]
        public void EnsureEditablePreview_CreatesBothStatObjectsWithoutPlayMode()
        {
            _view.EnsureEditableCardPreview();
            Assert.That(Stat("AttackValueText"), Is.Not.Null);
            Assert.That(Stat("HpValueText"), Is.Not.Null);
            Assert.That(Overlay().gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Show_AttachedEffectsAreBelowAndPlayerInfluencesAreAboveTheCard()
        {
            var state = BindBattleState();
            var unit = CreateUnit("unit", PlayerId.Player, 20, 40);
            unit.ApplyCombatStatBuff("Hero", attackBonus: 33);
            unit.RestoreFallout(2, 0);
            state.PlayerBoard.Place(unit.Position, unit);
            AddAmplification(state);
            _tile.Present(unit);

            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            var card = Overlay().Find("PreviewCardImage").GetComponent<RectTransform>();
            var attached = VisiblePanels("AttachedEffectTooltip");
            var influences = VisiblePanels("InfluenceTooltip");
            Assert.That(attached.Length, Is.EqualTo(2));
            Assert.That(influences.Length, Is.EqualTo(1));
            Assert.That(attached.Select(panel => panel.Find("Title").GetComponent<TMP_Text>().text),
                Does.Contain("용사: 공격력 +33"));
            Assert.That(attached.Select(panel => panel.Find("Title").GetComponent<TMP_Text>().text),
                Does.Contain("낙진"));
            Assert.That(influences[0].Find("Title").GetComponent<TMP_Text>().text,
                Is.EqualTo("마법 증폭진: 주문력 +10"));
            foreach (var panel in attached)
                Assert.That(panel.anchoredPosition.y + panel.rect.height * 0.5f,
                    Is.LessThan(card.anchoredPosition.y - card.rect.height * 0.5f));
            foreach (var panel in influences)
                Assert.That(panel.anchoredPosition.y - panel.rect.height * 0.5f,
                    Is.GreaterThan(card.anchoredPosition.y + card.rect.height * 0.5f));
            foreach (var panel in attached.Concat(influences))
            {
                var description = panel.Find("Description").GetComponent<TMP_Text>();
                Assert.That(description.text, Is.Empty);
                Assert.That(description.gameObject.activeSelf, Is.False);
                Assert.That(panel.rect.height, Is.LessThan(116f));
            }
            foreach (var text in TooltipRoot().GetComponentsInChildren<TMP_Text>())
                Assert.That(text.raycastTarget, Is.False);
            foreach (var panel in TooltipRoot().GetComponentsInChildren<UnityEngine.UI.Image>())
                Assert.That(panel.raycastTarget, Is.False);
        }

        [Test]
        public void Refresh_UpdatesSourceBuffAndHidesExpiredInfluenceDuringSameHold()
        {
            var state = BindBattleState();
            var unit = CreateUnit("unit", PlayerId.Player, 20, 40);
            unit.ApplyCombatStatBuff("Hero", attackBonus: 33);
            state.PlayerBoard.Place(unit.Position, unit);
            AddAmplification(state);
            _tile.Present(unit);
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);

            unit.ApplyCombatStatBuff("Hero", attackBonus: 33);
            state.PersistentEffects[0].Expire();
            RefreshPreview();

            var attached = VisiblePanels("AttachedEffectTooltip");
            Assert.That(attached.Length, Is.EqualTo(1));
            Assert.That(attached[0].Find("Title").GetComponent<TMP_Text>().text,
                Is.EqualTo("용사: 공격력 +66"));
            Assert.That(attached[0].Find("Description").gameObject.activeSelf, Is.False);
            Assert.That(VisiblePanels("InfluenceTooltip"), Is.Empty);
            _view.HideCurrentCardPreview();
            foreach (Transform panel in TooltipRoot())
                Assert.That(panel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Refresh_ReboundOccupantRemovesOldPooledStatusPanels()
        {
            var unit = CreateUnit("unit", PlayerId.Player, 20, 40);
            unit.RestoreFallout(2, 0);
            _tile.Present(unit);
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            Assert.That(VisiblePanels("AttachedEffectTooltip").Length, Is.EqualTo(1));

            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            RefreshPreview();

            Assert.That(VisiblePanels("AttachedEffectTooltip"), Is.Empty);
            Assert.That(VisiblePanels("InfluenceTooltip"), Is.Empty);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        [TestCase(2560f, 1080f)]
        public void Refresh_AllVisiblePanelsStayInsideCanvasAfterResize(float width, float height)
        {
            var state = BindBattleState();
            var unit = new UnitState("unit", "PreviewTestUnit", PlayerId.Player,
                new TileCoord(0, 0), AttackType.Melee, 20, 90, true, false, 0,
                hitsPerAttack: 2, hasBerserker: true, hasEndure: true,
                hasLifeSteal: true, hasRush: true, spellPower: 20);
            unit.ApplyCombatStatBuff("Hero", attackBonus: 33);
            unit.ApplyCombatStatBuff("RobotFusion", maxHpBonus: 20);
            unit.ApplyCombatStatBuff("Daehwandan", physicalDefenseBonus: 10);
            unit.RestoreFallout(2, 0);
            unit.CurrentHp -= 10;
            state.PlayerBoard.Place(unit.Position, unit);
            AddAmplification(state);
            _tile.Present(unit);
            Assert.That(_view.TryShowCurrentCardPreview(), Is.True);
            _canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            Canvas.ForceUpdateCanvases();
            RefreshPreview();

            var root = Overlay().GetComponent<RectTransform>();
            var rects = new List<Rect>();
            rects.Add(BoundsIn(root, root.Find("PreviewCardImage").GetComponent<RectTransform>()));
            foreach (Transform transform in TooltipRoot())
            {
                if (!transform.gameObject.activeSelf) continue;
                var panel = transform.GetComponent<RectTransform>();
                rects.Add(BoundsIn(root, panel));
                var title = panel.Find("Title").GetComponent<TMP_Text>();
                Assert.That(title.preferredHeight,
                    Is.LessThanOrEqualTo(title.rectTransform.rect.height + 1f));
                var description = panel.Find("Description").GetComponent<TMP_Text>();
                if (description.gameObject.activeSelf)
                    Assert.That(description.preferredHeight,
                        Is.LessThanOrEqualTo(description.rectTransform.rect.height + 1f));
            }
            for (var i = 0; i < rects.Count; i++)
            {
                Assert.That(rects[i].xMin, Is.GreaterThanOrEqualTo(root.rect.xMin - 0.1f));
                Assert.That(rects[i].xMax, Is.LessThanOrEqualTo(root.rect.xMax + 0.1f));
                Assert.That(rects[i].yMin, Is.GreaterThanOrEqualTo(root.rect.yMin - 0.1f));
                Assert.That(rects[i].yMax, Is.LessThanOrEqualTo(root.rect.yMax + 0.1f));
                for (var j = i + 1; j < rects.Count; j++)
                    Assert.That(rects[i].Overlaps(rects[j]), Is.False);
            }
        }


        [TestCase("normal")]
        [TestCase("release")]
        [TestCase("exit")]
        [TestCase("exit-release")]
        [TestCase("disable")]
        [TestCase("empty")]
        public void LongPress_OnlySuppressesItsOwnGesture(string ending)
        {
            _tile.Present(CreateUnit("unit", PlayerId.Player, 20, 40));
            var relay = _tile.gameObject.AddComponent<TileLongPressRelay>();
            relay.Initialize(_view, _tile);
            var pointer = new UnityEngine.EventSystems.PointerEventData(null);
            var clicks = 0;
            _tile.Clicked += _ => clicks++;

            if (ending != "normal")
            {
                relay.OnPointerDown(pointer);
                typeof(TileLongPressRelay).GetField("_pointerDownStartedAt",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(relay, Time.unscaledTime - 1f);
                typeof(TileLongPressRelay).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(relay, null);
                Assert.That(Overlay().gameObject.activeSelf, Is.True);

                if (ending == "disable")
                {
                    relay.enabled = false;
                    // EditMode does not dispatch this runtime lifecycle callback automatically.
                    typeof(TileLongPressRelay).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(relay, null);
                    relay.enabled = true;
                }
                else
                {
                    if (ending == "exit" || ending == "exit-release" || ending == "empty")
                        relay.OnPointerExit(pointer);
                    relay.OnPointerUp(pointer);
                    if (ending == "release" || ending == "exit-release")
                        relay.OnPointerClick(pointer);
                }
                Assert.That(clicks, Is.Zero, "The preview gesture must not select or attack.");
                Assert.That(Overlay().gameObject.activeSelf, Is.False);
                if (ending == "empty")
                    _tile.Present(null);
            }

            for (var expected = 1; expected <= 3; expected++)
            {
                relay.OnPointerDown(pointer);
                relay.OnPointerUp(pointer);
                relay.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(expected), "Each new short press must reach TileView.");
            }
        }

        private BattleState BindBattleState()
        {
            var deck = Enumerable.Repeat("Goblin", 33).ToArray();
            var battle = new BattleSetupService().CreateInitialState(
                new BattleSetupRequest(deck, deck, PlayerId.Player));
            var flow = new BattleFlowController();
            flow.RestoreBattleState(battle);
            var bootstrapperObject = new GameObject("PreviewTestBootstrapper");
            bootstrapperObject.transform.SetParent(_canvasObject.transform, false);
            bootstrapperObject.SetActive(false);
            var bootstrapper = bootstrapperObject.AddComponent<BattleBootstrapper>();
            typeof(BattleBootstrapper).GetField("_battleGateway",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bootstrapper, new LocalBattleGateway(flow));
            SetField("_battleBootstrapper", bootstrapper);
            return battle;
        }

        private static void AddAmplification(BattleState state)
        {
            state.PersistentEffects.Add(new PersistentEffectState(
                MagicAmplificationCircleRules.CardId, PlayerId.Player,
                MagicAmplificationCircleRules.EffectId, state.TurnNumber, "", new ResourceSet()));
        }

        private Transform TooltipRoot() => Overlay().Find("PreviewSpecialEffectTooltips");

        private RectTransform[] VisiblePanels(string prefix) =>
            TooltipRoot().Cast<Transform>().Where(panel => panel.name.StartsWith(prefix) &&
                panel.gameObject.activeSelf).Select(panel => panel.GetComponent<RectTransform>()).ToArray();

        private static Rect BoundsIn(RectTransform root, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var min = (Vector2)root.InverseTransformPoint(corners[0]);
            var max = min;
            foreach (var corner in corners)
            {
                var local = (Vector2)root.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static UnitState CreateUnit(string id, PlayerId owner, int attack, int hp)
        {
            return new UnitState(id, "PreviewTestUnit", owner, new TileCoord(0, 0),
                AttackType.Melee, attack, hp, true, false, 0);
        }

        private void SetField(string name, object value)
        {
            typeof(TileTextView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, value);
        }

        private void RefreshPreview()
        {
            typeof(TileTextView).GetMethod("RefreshCurrentCardPreview",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, null);
        }

        private Transform Overlay() => _canvasObject.transform.Find("CardPreviewOverlay");

        private TMP_Text Stat(string name) =>
            Overlay().Find("PreviewCardImage/" + name).GetComponent<TMP_Text>();

        private void AssertPosition(string name, Vector2 expected)
        {
            var actual = Stat(name).rectTransform.anchoredPosition;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }
    }
}
