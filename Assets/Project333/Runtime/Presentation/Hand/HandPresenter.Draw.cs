using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandPresenter
    {
        private sealed class PendingDraw
        {
            public string RuntimeId;
            public CardDrawFlight.Pose Origin;
            public RectTransform OriginRect;
            public Sprite Back;
            public CardDrawFlight Flight;
            public HandCardTextView Target;
            public float LayoutDeadline = -1;
        }
        private readonly HashSet<string> _drawKnownIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PendingDraw> _draws = new List<PendingDraw>();
        private readonly CardDrawOriginCache _drawOriginCache = new CardDrawOriginCache();
        private PlayerDeckPresenter _drawDeck;
        private BattleMulliganOverlayPresenter _drawMulligan;
        private bool _drawHasBaseline;
        private bool _drawWasMulligan;
        private int _drawPreviousCount;
        private int _drawCredits;

        public void ConfigureDeckDrawSource(PlayerDeckPresenter deck, BattleMulliganOverlayPresenter mulligan = null)
        { if (_drawDeck != deck) _drawOriginCache.Clear(); _drawDeck = deck; _drawMulligan = mulligan; }

        // Authoritative draw events give permission; only new hand RuntimeIds can consume it.
        public void QueueCardDraws(int count)
        {
            if (UnityEngine.Application.isPlaying && isActiveAndEnabled && count > 0)
                _drawCredits = Mathf.Min(PlayerState.AbsoluteMaxHandSize, _drawCredits + count);
        }

        private void PlanCardDraws(HandState hand)
        {
            if (hand == null) { CancelCardDraws(); return; }
            // Warm once when battle UI has valid geometry; later hand refreshes reuse it.
            if (_drawDeck != null && _drawDeck.RemainingDeckCount > 0 && _runtimeCanvasRect != null)
                _drawOriginCache.TryGetPose(_drawDeck.DrawSourceRect, _runtimeCanvasRect, out _);
            for (var i = _draws.Count - 1; i >= 0; i--)
                if (!ContainsDrawId(hand, _draws[i].RuntimeId)) RemoveDraw(i);

            var duringMulligan = PreviewBattleState?.Phase == PhaseType.Mulligan;
            // Mulligan redraws already have their own overlay animation. Only the appended
            // first-turn draw may fly into hand when replacement and turn start arrive together.
            if (duringMulligan) _drawCredits = 0;
            else if (_drawWasMulligan)
                _drawCredits = Mathf.Min(_drawCredits, Mathf.Max(0, hand.Count - _drawPreviousCount));

            if (_drawHasBaseline && _drawCredits > 0 && _drawDeck != null && _runtimeCanvasRect != null)
            {
                var added = 0;
                // Normal draws append to hand; mulligan replacements can also introduce earlier IDs.
                for (var i = hand.Cards.Count - 1; i >= 0 && added < _drawCredits; i--)
                {
                    var card = hand.Cards[i];
                    if (card.IsTemporaryReplicate || _drawKnownIds.Contains(card.RuntimeId)) continue;
                    var originRect = _drawDeck.DrawSourceRect;
                    if (!_drawOriginCache.TryGetPose(originRect, _runtimeCanvasRect, out var origin)) continue;
                    _draws.Insert(0, new PendingDraw { RuntimeId = card.RuntimeId, Origin = origin,
                        OriginRect = originRect, Back = _drawDeck.DrawCardBackSprite });
                    added++;
                }
                _drawCredits -= added;
            }
            else if (!_drawHasBaseline) _drawCredits = 0; // Joining/reconnecting never replays the current hand.

            _drawKnownIds.Clear();
            foreach (var card in hand.Cards) _drawKnownIds.Add(card.RuntimeId);
            _drawHasBaseline = true;
            _drawWasMulligan = duringMulligan;
            _drawPreviousCount = hand.Count;
            ApplyDrawSuppressionToViews();
        }

        private static bool ContainsDrawId(HandState hand, string runtimeId)
        {
            foreach (var card in hand.Cards) if (card.RuntimeId == runtimeId) return true;
            return false;
        }

        private void ApplyDrawSuppressionToViews()
        {
            foreach (var draw in _draws) draw.Target = null;
            foreach (var card in _handCardViews)
            {
                if (card == null) continue;
                var text = card.GetComponent<HandCardTextView>();
                string hiddenId = null;
                foreach (var draw in _draws)
                    if (card.HasCard && card.RuntimeId == draw.RuntimeId)
                    { hiddenId = draw.RuntimeId; draw.Target = text; break; }
                text?.SetDrawSuppression(hiddenId);
            }
        }

        private void TickCardDraws()
        {
            if (_draws.Count == 0 || (_drawMulligan != null && _drawMulligan.IsSessionActive)) return;
            var delay = 0f;
            for (var i = 0; i < _draws.Count; i++)
            {
                var draw = _draws[i];
                var artwork = draw.Target == null ? null : draw.Target.PrepareDrawTarget();
                if (artwork == null || artwork.overrideSprite == null) { RemoveDraw(i--); continue; }
                // Newly activated stretched artwork can have a zero rect until the next Canvas pass.
                if (!CardDrawFlight.Pose.TryCapture(artwork.rectTransform, _runtimeCanvasRect, out _))
                {
                    if (draw.LayoutDeadline < 0) draw.LayoutDeadline = Time.unscaledTime + 1f;
                    if (Time.unscaledTime > draw.LayoutDeadline) RemoveDraw(i--);
                    continue;
                }
                if (draw.Flight == null)
                {
                    draw.Flight = CardDrawFlight.Create(_runtimeCanvasRect, draw.Origin, draw.Back, artwork, delay, draw.OriginRect, _drawOriginCache);
                    delay += CardDrawFlight.Stagger;
                }
                if (draw.Flight == null || draw.Flight.Tick(artwork)) RemoveDraw(i--);
            }
            ApplyDrawSuppressionToViews();
        }

        private void RemoveDraw(int index)
        {
            var flight = _draws[index].Flight;
            if (flight != null)
            { flight.gameObject.SetActive(false); if (UnityEngine.Application.isPlaying) Destroy(flight.gameObject); else DestroyImmediate(flight.gameObject); }
            _draws.RemoveAt(index);
        }

        public void CancelCardDraws()
        {
            for (var i = _draws.Count - 1; i >= 0; i--) RemoveDraw(i);
            _drawOriginCache.Clear();
            _drawKnownIds.Clear(); _drawHasBaseline = false; _drawCredits = 0;
            _drawWasMulligan = false; _drawPreviousCount = 0;
            ApplyDrawSuppressionToViews();
        }
    }
}
