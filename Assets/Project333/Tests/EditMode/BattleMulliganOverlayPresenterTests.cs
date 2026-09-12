using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project333.Tests.EditMode
{
    public sealed class BattleMulliganOverlayPresenterTests
    {
        [TestCase(true, "선공\n시작 카드를 선택하세요")]
        [TestCase(false, "후공\n시작 카드를 선택하세요")]
        public void BuildTurnOrderTitle_UsesLocalPlayerPerspective(
            bool isLocalFirstPlayer,
            string expected)
        {
            var result = BattleMulliganOverlayPresenter.BuildTurnOrderTitle(
                isLocalFirstPlayer,
                "시작 카드를 선택하세요");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildConfirmedCardLayout_ReplacesSelectedSlotAndIgnoresTurnStartDraw()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "B", "C" },
                new HashSet<int> { 1 },
                new[] { "A", "C", "D", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "A", "D", "C" }));
        }

        [Test]
        public void BuildConfirmedCardLayout_WithDuplicateCards_PreservesUnselectedCopy()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "A", "B", "C" },
                new HashSet<int> { 0, 2 },
                new[] { "A", "C", "X", "Y", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "X", "A", "Y", "C" }));
        }

        [Test]
        public void BuildConfirmedCardLayout_WithNoSelectedCards_KeepsOpeningLayout()
        {
            var result = BattleMulliganOverlayPresenter.BuildConfirmedCardLayout(
                new[] { "A", "B", "C" },
                new HashSet<int>(),
                new[] { "A", "B", "C", "TURN_DRAW" });

            Assert.That(result, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void TryBuildCardStatValues_UnitUsesOwnedCardUpgradeBonuses()
        {
            var definition = new UnitCardDefinition(
                cardId: "unit",
                displayName: "Unit",
                cost: new ResourceSet(mana: 0, qi: 0, power: 0, gold: 0),
                attackType: AttackType.Melee,
                attack: 20,
                health: 30,
                canMove: true,
                isScience: false,
                sciencePowerUpkeep: 0);

            var hasStats = BattleMulliganOverlayPresenter.TryBuildCardStatValues(
                definition,
                upgradeLevel: 5,
                out var attack,
                out var hp);

            Assert.That(hasStats, Is.True);
            Assert.That(attack, Is.EqualTo(21));
            Assert.That(hp, Is.EqualTo(34));
        }

        [Test]
        public void TryBuildCardStatValues_DamageSpellUsesAttackBadgeAndHasNoHp()
        {
            var definition = new DamageSpellCardDefinition(
                cardId: "spell",
                displayName: "Spell",
                cost: new ResourceSet(mana: 1, qi: 0, power: 0, gold: 0),
                damage: 10);

            var hasStats = BattleMulliganOverlayPresenter.TryBuildCardStatValues(
                definition,
                upgradeLevel: 5,
                out var damage,
                out var hp);

            Assert.That(hasStats, Is.True);
            Assert.That(damage, Is.EqualTo(15));
            Assert.That(hp, Is.Zero);
        }
    }

    public sealed class BattleMulliganStatFontTests
    {
        private GameObject _root;
        private BattleMulliganOverlayPresenter _presenter;
        private Button[] _buttons;
        private Text[] _attackTexts;
        private Text[] _hpTexts;
        private Font _attackFont;
        private Font _hpFont;

        [SetUp]
        public void SetUp()
        {
            _attackFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf");
            _hpFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            Assert.That(_attackFont, Is.Not.Null);
            Assert.That(_hpFont, Is.Not.Null);
            _root = new GameObject("MulliganFontTest", typeof(RectTransform));
            _root.SetActive(false);
            _presenter = _root.AddComponent<BattleMulliganOverlayPresenter>();
            _buttons = new Button[3];
            _attackTexts = new Text[3];
            _hpTexts = new Text[3];
            for (var index = 0; index < _buttons.Length; index++)
            {
                var card = new GameObject("Card_" + index, typeof(RectTransform), typeof(Button));
                card.transform.SetParent(_root.transform, false);
                _buttons[index] = card.GetComponent<Button>();
                _attackTexts[index] = CreateStatText(card.transform, "AttackValueText", _attackFont);
                _hpTexts[index] = CreateStatText(card.transform, "HpValueText", _hpFont);
            }

            SetField("_cardButtons", _buttons);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InitializeAndReenable_PreserveEveryInspectorStatFont(bool referencesAssigned)
        {
            if (referencesAssigned)
            {
                SetField("_attackValueTexts", _attackTexts);
                SetField("_hpValueTexts", _hpTexts);
            }

            Invoke("Awake");
            Invoke("OnEnable");
            _presenter.Bind(null);
            Invoke("EnsureCardStatTexts");
            Invoke("RefreshCardStatLayouts");
            Invoke("OnEnable");

            for (var index = 0; index < _buttons.Length; index++)
            {
                Assert.That(_attackTexts[index].font, Is.SameAs(_attackFont));
                Assert.That(_hpTexts[index].font, Is.SameAs(_hpFont));
                Assert.That(_buttons[index].GetComponentsInChildren<Text>(true).Length, Is.EqualTo(2));
            }
        }

        [Test]
        public void Reinitialize_KeepsFontChangedAfterFirstInitialization()
        {
            Invoke("Awake");
            _attackTexts[0].font = _hpFont;
            _hpTexts[2].font = _attackFont;

            _presenter.Bind(null);
            Invoke("OnEnable");
            Invoke("RefreshCardStatLayouts");

            Assert.That(_attackTexts[0].font, Is.SameAs(_hpFont));
            Assert.That(_hpTexts[2].font, Is.SameAs(_attackFont));
            Assert.That(_attackTexts[1].font, Is.SameAs(_attackFont));
            Assert.That(_hpTexts[1].font, Is.SameAs(_hpFont));
        }

        [Test]
        public void Initialize_FillsOnlyMissingFonts()
        {
            _attackTexts[0].font = null;
            _hpTexts[1].font = null;

            Invoke("Awake");
            Invoke("OnEnable");

            Assert.That(_attackTexts[0].font, Is.Not.Null);
            Assert.That(_hpTexts[1].font, Is.Not.Null);
            Assert.That(_attackTexts[1].font, Is.SameAs(_attackFont));
            Assert.That(_hpTexts[0].font, Is.SameAs(_hpFont));
        }

        [Test]
        public void Initialize_CreatesMissingStatTextsWithFonts()
        {
            Object.DestroyImmediate(_attackTexts[0].gameObject);
            Object.DestroyImmediate(_hpTexts[0].gameObject);

            Invoke("Awake");
            Invoke("OnEnable");

            var attack = _buttons[0].transform.Find("AttackValueText").GetComponent<Text>();
            var hp = _buttons[0].transform.Find("HpValueText").GetComponent<Text>();
            Assert.That(attack.font, Is.Not.Null);
            Assert.That(hp.font, Is.Not.Null);
            Assert.That(attack.raycastTarget, Is.False);
            Assert.That(hp.raycastTarget, Is.False);
            Assert.That(_buttons[0].GetComponentsInChildren<Text>(true).Length, Is.EqualTo(2));
        }

        private static Text CreateStatText(Transform parent, string name, Font font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            return text;
        }

        private void SetField(string name, object value)
        {
            typeof(BattleMulliganOverlayPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_presenter, value);
        }

        private void Invoke(string method)
        {
            typeof(BattleMulliganOverlayPresenter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_presenter, null);
        }
    }
}
