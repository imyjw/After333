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
