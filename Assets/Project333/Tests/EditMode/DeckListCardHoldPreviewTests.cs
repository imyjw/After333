using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Draft;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project333.Tests.EditMode
{
    public sealed class DeckListCardHoldPreviewTests
    {
        private GameObject _root;
        private GameObject _events;
        private Text _text;
        private readonly Dictionary<string, CardDefinitionAsset> _cards = new Dictionary<string, CardDefinitionAsset>();
        private DeckListCardHoldPreview Relay => _text.GetComponent<DeckListCardHoldPreview>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DeckPreviewTestCanvas", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _events = new GameObject("DeckPreviewTestEvents", typeof(EventSystem));
            var textObject = new GameObject("DeckList", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(_root.transform, false);
            _text = textObject.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 20;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.rectTransform.sizeDelta = new Vector2(1200, 2000);
            AddCard("cheap", "Alpha", 1);
            AddCard("expensive", "Zulu", 9);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_events);
            foreach (var card in _cards.Values) Object.DestroyImmediate(card);
            _cards.Clear();
        }

        [Test]
        public void SortedGroupedRows_ResolveTheDisplayedCardRatherThanInputOrder()
        {
            Configure(new[] { "cheap", "expensive", "cheap" });
            Assert.That(_text.text.IndexOf("Zulu", StringComparison.Ordinal),
                Is.LessThan(_text.text.IndexOf("Alpha", StringComparison.Ordinal)));
            Assert.That(_text.text, Does.Contain("2x Alpha"));
            AssertPressedCardAt("Zulu", "expensive");
            AssertPressedCardAt("Alpha", "cheap");
        }

        [Test]
        public void SummaryHeadersAndEmptyAreas_AreNotCardRows()
        {
            Configure(new[] { "cheap", "expensive" }, summary: true);
            AssertPressedCardAt("DECK", null);
            AssertPressedCardAt("CARD LIST", null);
            AssertPressedCardAt("Zulu", "expensive");
            AssertPressedCardAt("Alpha", "cheap");
            var point = PointForText("Alpha");
            point.x += 600;
            Relay.OnPointerDown(Pointer(point));
            Assert.That(HeldCard(), Is.Null, "Unused space after the card text is not a card.");
        }

        [Test]
        public void WrappedName_ResolvesItsContinuationLineToTheSameCard()
        {
            AddCard("long", "Bravo Charlie Delta Echo Foxtrot Golf Hotel India Juliet Kilo Lima", 5);
            _text.rectTransform.sizeDelta = new Vector2(135, 2000);
            Configure(new[] { "long", "cheap" });
            PopulateText();
            Assert.That(_text.cachedTextGenerator.lines.Count, Is.GreaterThan(3));
            AssertPressedCardAt("Bravo", "long");
            AssertPressedCardAt("Hotel", "long");
            AssertPressedCardAt("Lima", "long");
            AssertPressedCardAt("Alpha", "cheap");
        }

        [Test]
        public void KoreanCardNames_ResolveWithTheGameFontAndWrappedRows()
        {
            _text.font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/TextMesh Pro/Fonts/Noto_Sans_KR/static/NotoSansKR-Regular.ttf");
            Assert.That(_text.font, Is.Not.Null);
            AddCard("mana", "마나의 샘", 5);
            AddCard("cerberus", "케르베로스", 8);
            AddCard("herb", "만년설삼", 2);
            _text.rectTransform.sizeDelta = new Vector2(110, 2000);
            Configure(new[] { "herb", "mana", "cerberus", "mana" }, summary: true);
            AssertPressedCardAt("CARD LIST", null);
            AssertPressedCardAt("마나", "mana");
            AssertPressedCardAt("샘", "mana");
            AssertPressedCardAt("케르", "cerberus");
            AssertPressedCardAt("로스", "cerberus");
            AssertPressedCardAt("만년설삼", "herb");
        }
        [Test]
        public void EmptyListAndUnrelatedText_DoNotStartAPreview()
        {
            Configure(Array.Empty<string>());
            AssertPressedCardAt("Empty", null);
            Configure(new[] { "cheap" });
            _text.text = "Unrelated";
            AssertPressedCardAt("Unrelated", null);
        }

        [Test]
        public void HoldDelay_QuickTapDoesNotShow_ReleaseHidesImmediately()
        {
            Configure(new[] { "cheap" });
            var pointer = Pointer(PointForText("Alpha"));
            Relay.OnPointerDown(pointer);
            Advance(0.39f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Relay.OnPointerUp(pointer);
            Advance(0.5f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Relay.OnPointerDown(pointer);
            Advance(0.41f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.True);
            Relay.OnPointerUp(pointer);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Assert.That(HeldCard(), Is.Null);
        }

        [Test]
        public void OnlyPrimaryButtonAndOwningPointer_ControlThePreview()
        {
            Configure(new[] { "cheap" });
            var pointer = Pointer(PointForText("Alpha"), 11);
            pointer.button = PointerEventData.InputButton.Right;
            Relay.OnPointerDown(pointer);
            Assert.That(HeldCard(), Is.Null);
            pointer.button = PointerEventData.InputButton.Left;
            Relay.OnPointerDown(pointer);
            Advance(0.41f);
            var other = Pointer(pointer.position, 12);
            Relay.OnPointerUp(other);
            Relay.OnPointerExit(other);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.True);
            Relay.OnPointerUp(pointer);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
        }

        [TestCase("exit")]
        [TestCase("move")]
        [TestCase("drag")]
        [TestCase("focus")]
        [TestCase("pause")]

        [TestCase("hideGroup")]
        public void InterruptedHold_HidesImmediatelyAndDoesNotReappear(string interruption)
        {
            Configure(new[] { "cheap" });
            var pointer = Pointer(PointForText("Alpha"));
            Relay.OnPointerDown(pointer);
            Advance(0.41f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.True);
            switch (interruption)
            {
                case "exit": Relay.OnPointerExit(pointer); break;
                case "move":
                    pointer.position += Vector2.right * 100;
                    Relay.OnPointerMove(pointer);
                    break;
                case "drag": Relay.OnBeginDrag(pointer); break;
                case "focus": Invoke("OnApplicationFocus", false); break;
                case "pause": Invoke("OnApplicationPause", true); break;

                case "hideGroup":
                    _root.AddComponent<CanvasGroup>().alpha = 0;
                    Advance(0.42f);
                    break;
            }
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Advance(1f);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Assert.That(HeldCard(), Is.Null);
        }

        [Test]
        public void UnchangedRefreshPreservesHold_ChangedRowsCancelIt()
        {
            Configure(new[] { "cheap" });
            var pointer = Pointer(PointForText("Alpha"));
            Relay.OnPointerDown(pointer);
            Configure(new[] { "cheap" });
            Assert.That(HeldCard(), Is.EqualTo("cheap"));
            Advance(0.41f);
            Configure(new[] { "cheap" });
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.True);
            Configure(new[] { "expensive", "cheap" });
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);
            Assert.That(HeldCard(), Is.Null);
        }

        [Test]
        public void ScrollViewport_RejectsClippedRowsAndAcceptsTheSameRowAfterScrolling()
        {
            var scroll = CreateScrollView();
            var ids = new List<string>();
            for (var i = 0; i < 16; i++)
            {
                var id = "row" + i.ToString("00");
                AddCard(id, "Name" + i.ToString("00"), 16 - i);
                ids.Add(id);
            }
            Configure(ids);
            Canvas.ForceUpdateCanvases();
            AssertPressedCardAt("Name00", "row00");
            AssertPressedCardAt("Name15", null);
            var pointer = Pointer(PointForText("Name00"));
            Relay.OnPointerDown(pointer);
            Advance(0.41f);
            scroll.onValueChanged.Invoke(Vector2.zero);
            Assert.That(DeckCardPreviewOverlay.IsVisibleFor(Relay), Is.False);

            var index = _text.text.IndexOf("Name15", StringComparison.Ordinal);
            var line = LineForCharacter(index);
            _text.rectTransform.anchoredPosition = new Vector2(0, -line.topY + 5);
            AssertPressedCardAt("Name15", "row15");
        }

        [Test]
        public void DragAndWheelEvents_ReachTheExistingScrollRect()
        {
            var scroll = CreateScrollView();
            Configure(new[] { "cheap" });
            Canvas.ForceUpdateCanvases();
            var pointer = Pointer(PointForText("Alpha"));
            Relay.OnPointerDown(pointer);
            Relay.OnInitializePotentialDrag(pointer);
            Relay.OnBeginDrag(pointer);
            pointer.position += Vector2.up * 60;
            pointer.delta = Vector2.up * 60;
            Relay.OnDrag(pointer);
            Relay.OnEndDrag(pointer);
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(20));
            Assert.That(HeldCard(), Is.Null);
            var before = scroll.content.anchoredPosition.y;
            pointer.scrollDelta = Vector2.down;
            Relay.OnScroll(pointer);
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(before));
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RuntimeDisable_HidesPreviewInTheSameFrame()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new UnityEngine.TestTools.EnterPlayMode();
            VerifyRuntimeDisable();
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        // Keep captured delegates outside the iterator that crosses EnterPlayMode's domain reload.
        private static void VerifyRuntimeDisable()
        {
            var canvasObject = new GameObject("RuntimeDeckPreviewCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var listObject = new GameObject("RuntimeDeckList", typeof(RectTransform), typeof(Text));
            listObject.transform.SetParent(canvasObject.transform, false);
            var text = listObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var card = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            card.ConfigureBaseForTests("runtime", "Runtime Card", new ResourceSetData(1, 0, 0, 0));
            DeckListCardHoldPreview.Configure(text, new[] { "runtime" }, _ => card, "Empty");
            var relay = listObject.GetComponent<DeckListCardHoldPreview>();
            try
            {
                DeckCardPreviewOverlay.ShowFor(relay, "runtime", card, text.font);
                Assert.That(DeckCardPreviewOverlay.IsVisibleFor(relay), Is.True);
                relay.enabled = false;
                Assert.That(DeckCardPreviewOverlay.IsVisibleFor(relay), Is.False,
                    "Disabling the component must hide the card without a later Update or pointer-up.");
                relay.enabled = true;
                DeckCardPreviewOverlay.ShowFor(relay, "runtime", card, text.font);
                listObject.SetActive(false);
                Assert.That(DeckCardPreviewOverlay.IsVisibleFor(relay), Is.False,
                    "Disabling the list hierarchy must also hide the card in the same frame.");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(card);
            }
        }

        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator LeavePlayModeAfterLifecycleTest()
        {
            if (UnityEngine.Application.isPlaying) yield return new UnityEngine.TestTools.ExitPlayMode();
        }
        private ScrollRect CreateScrollView()
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_root.transform, false);
            var rect = viewport.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 120);
            _text.rectTransform.SetParent(rect, false);
            _text.rectTransform.anchorMin = _text.rectTransform.anchorMax = new Vector2(0, 1);
            _text.rectTransform.pivot = new Vector2(0, 1);
            _text.rectTransform.sizeDelta = new Vector2(400, 2000);
            _text.rectTransform.anchoredPosition = Vector2.zero;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = rect;
            scroll.content = _text.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return scroll;
        }

        private void AddCard(string id, string displayName, int cost)
        {
            var card = ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
            card.ConfigureBaseForTests(id, displayName, new ResourceSetData(cost, 0, 0, 0));
            _cards.Add(id, card);
        }

        private void Configure(IReadOnlyList<string> ids, bool summary = false)
        {
            DeckListCardHoldPreview.Configure(_text, ids, id => _cards.TryGetValue(id, out var c) ? c : null,
                "Empty", includeSummary: summary);
            PopulateText();
        }

        private void PopulateText()
        {
            Canvas.ForceUpdateCanvases();
            _text.cachedTextGenerator.Populate(_text.text, _text.GetGenerationSettings(_text.rectTransform.rect.size));
        }

        private UILineInfo LineForCharacter(int index)
        {
            var lines = _text.cachedTextGenerator.lines;
            for (var i = lines.Count - 1; i >= 0; i--)
                if (lines[i].startCharIdx <= index) return lines[i];
            throw new InvalidOperationException("No rendered line for source character.");
        }

        private Vector2 PointForText(string word)
        {
            PopulateText();
            var index = _text.text.IndexOf(word, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var character = _text.cachedTextGenerator.characters[index];
            var line = LineForCharacter(index);
            var local = new Vector2(character.cursorPos.x + character.charWidth * 0.5f,
                line.topY - line.height * 0.5f) / _text.pixelsPerUnit;
            return RectTransformUtility.WorldToScreenPoint(null, _text.rectTransform.TransformPoint(local));
        }

        private PointerEventData Pointer(Vector2 position, int id = -1) => new PointerEventData(_events.GetComponent<EventSystem>())
        {
            position = position,
            pressPosition = position,
            pointerId = id,
            button = PointerEventData.InputButton.Left,
        };

        private void AssertPressedCardAt(string word, string expected)
        {
            var pointer = Pointer(PointForText(word));
            Relay.OnPointerDown(pointer);
            Assert.That(HeldCard(), Is.EqualTo(expected), word);
            Relay.OnPointerUp(pointer);
        }

        private string HeldCard() => (string)typeof(DeckListCardHoldPreview)
            .GetField("_heldCardId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Relay);

        private void Advance(float elapsed)
        {
            var started = (float)typeof(DeckListCardHoldPreview)
                .GetField("_pressedAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Relay);
            Invoke("AdvanceHold", started + elapsed);
        }

        private void Invoke(string name, object value) => typeof(DeckListCardHoldPreview)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Relay, new[] { value });
    }
}
