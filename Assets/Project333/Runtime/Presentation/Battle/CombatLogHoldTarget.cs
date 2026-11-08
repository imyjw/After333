using System.Collections.Generic;
using Project333.Runtime.Application.Online;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project333.Runtime.Presentation.Battle
{
    // Lives on the TMP object so pointer events work even when the log controller is a sibling.
    // No drag handler: the parent ScrollRect retains its usual drag/scroll ownership.
    public sealed class CombatLogHoldTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float HoldSeconds = .25f;
        private TMP_Text _text;
        private BattleBootstrapper _source;
        private CombatLogAttackPreview _preview;
        private BattleCombatLogEntryDto _heldEntry;
        private int _pointerId;
        private Vector2 _pressPosition;
        private float _pressedAt;
        private bool _pressed;
        internal bool IsHolding => _pressed;

        public void Bind(TMP_Text text, BattleBootstrapper source) { _text = text; _source = source; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pressed || eventData.button != PointerEventData.InputButton.Left || _text == null || _source == null) return;
            var link = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.pressEventCamera);
            if (link < 0) return;
            var id = _text.textInfo.linkInfo[link].GetLinkID();
            if (!id.StartsWith("combat-", System.StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(7), out var index) || !_source.TryGetCombatLogAttackPreview(index, out var entry) ||
                !entry.HasHoldPreview) return;

            _heldEntry = CloneForHold(entry);
            _pressPosition = eventData.position; _pointerId = eventData.pointerId;
            _pressedAt = Time.unscaledTime; _pressed = true;
        }
        private static BattleCombatLogEntryDto CloneForHold(BattleCombatLogEntryDto entry)
        {
            var targets = new List<BattleCombatLogCardSnapshotDto>();
            if (entry.TargetPreviews != null)
            {
                for (var i = 0; i < entry.TargetPreviews.Count; i++)
                {
                    if (entry.TargetPreviews[i] != null) targets.Add(entry.TargetPreviews[i].Clone());
                }
            }
            return new BattleCombatLogEntryDto
            {
                EntryType = entry.EntryType,
                SourceOwnerId = entry.SourceOwnerId,
                TargetOwnerId = entry.TargetOwnerId,
                SourceCardId = entry.SourceCardId,
                TargetCardId = entry.TargetCardId,
                SourcePreview = entry.SourcePreview?.Clone(),
                TargetPreview = entry.TargetPreview?.Clone(),
                TargetPreviews = targets
            };
        }
        private void Update()
        {
            if (!_pressed) return;
            var position = (Vector2)Input.mousePosition;
            if (_pointerId >= 0)
            {
                var found = false;
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _pointerId) continue;
                    if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended) { CancelHold(); return; }
                    position = touch.position; found = true; break;
                }
                if (!found) { CancelHold(); return; }
            }
            AdvanceHold(Time.unscaledTime, position);
        }
        internal void AdvanceHold(float now, Vector2 position)
        {
            if (!_pressed) return;
            var threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            if ((position - _pressPosition).sqrMagnitude > threshold * threshold) { CancelHold(); return; }
            if (now - _pressedAt < HoldSeconds || (_preview != null && _preview.IsVisible)) return;
            if (_preview == null) _preview = CombatLogAttackPreview.Create(gameObject.scene, _text.font);
            _preview.Show(_heldEntry);
        }
        public void OnPointerUp(PointerEventData e) { if (_pressed && e.pointerId == _pointerId) CancelHold(); }
        public void OnPointerExit(PointerEventData e) { if (_pressed && e.pointerId == _pointerId) CancelHold(); }
        public void CancelHold()
        {
            _pressed = false; _heldEntry = null;
            if (_preview != null) _preview.Hide();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelHold(); }
        private void OnDisable() => CancelHold();
        private void OnDestroy()
        {
            CancelHold();
            if (_preview == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_preview.gameObject); else DestroyImmediate(_preview.gameObject);
        }
    }
}
