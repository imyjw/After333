using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandPresenter
    {
        private sealed class PendingSpellUse
        {
            public string CardId, RuntimeId;
            public SpellCardUseVfx View;
            public bool Accepted;
            public bool IsTrap;
        }
        private readonly List<PendingSpellUse> _pendingSpellUses = new List<PendingSpellUse>();
        private readonly List<SpellCardUseVfx> _spellUseViews = new List<SpellCardUseVfx>();

        public void PrepareSpellCardUse(string cardId, string runtimeId) => PrepareCardUse(cardId, runtimeId, false);
        public void PrepareTrapCardUse(string cardId, string runtimeId) => PrepareCardUse(cardId, runtimeId, true);

        private void PrepareCardUse(string cardId, string runtimeId, bool isTrap)
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled) return;
            foreach (var card in _handCardViews)
            {
                if (card == null || !card.HasCard || card.CardId != cardId ||
                    (!string.IsNullOrEmpty(runtimeId) && card.RuntimeId != runtimeId)) continue;
                foreach (var pending in _pendingSpellUses)
                    if (pending.RuntimeId == card.RuntimeId) return;
                var view = SpellCardUseVfx.Capture(card.GetComponent<HandCardTextView>()?.TutorialArtwork);
                if (view != null)
                {
                    _pendingSpellUses.Add(new PendingSpellUse { CardId=cardId, RuntimeId=card.RuntimeId, View=view, IsTrap=isTrap });
                    view.Hold();
                    ApplySpellUseSuppressionToViews();
                }
                return;
            }
        }

        // Start on authoritative confirmation, independently of delayed area-impact/hand presentation.
        // Online SpellCast lacks hand RuntimeId; requests of the same CardId retain their submission order.
        public void ConfirmSpellCardUse(string cardId, string runtimeId = null)
        {
            foreach (var pending in _pendingSpellUses)
                if (!pending.Accepted && pending.CardId == cardId &&
                    (string.IsNullOrEmpty(runtimeId) || pending.RuntimeId == runtimeId))
                {
                    pending.Accepted = true;
                    if (pending.View != null)
                    {
                        pending.View.Begin();
                        _spellUseViews.Add(pending.View);
                    }
                    return;
                }
        }

        // Trap installation ACK deliberately contains no public card identity, even for its owner.
        public void ConfirmTrapCardUse()
        {
            foreach (var pending in _pendingSpellUses)
                if (pending.IsTrap && !pending.Accepted)
                { ConfirmSpellCardUse(pending.CardId, pending.RuntimeId); return; }
        }

        private void PresentSpellCardUses(HandState hand)
        {
            if (hand == null) { CancelSpellCardUses(); return; }
            _spellUseViews.RemoveAll(view => view == null);
            for (var i = _pendingSpellUses.Count - 1; i >= 0; i--)
            {
                var pending = _pendingSpellUses[i];
                var stillPresent = false;
                foreach (var card in hand.Cards)
                    if (string.Equals(card.RuntimeId, pending.RuntimeId, StringComparison.Ordinal))
                    { stillPresent = true; break; }
                if (stillPresent) continue;
                _pendingSpellUses.RemoveAt(i);
                // Hand refresh only releases suppression. It must never start/restart breakup.
                if (!pending.Accepted) RemoveSpellUse(pending.View);
            }
        }

        private void ApplySpellUseSuppressionToViews()
        {
            foreach (var card in _handCardViews)
            {
                if (card == null) continue;
                string runtimeId = null;
                if (card.HasCard)
                    foreach (var pending in _pendingSpellUses)
                        if (string.Equals(pending.RuntimeId, card.RuntimeId, StringComparison.Ordinal))
                        { runtimeId = pending.RuntimeId; break; }
                card.GetComponent<HandCardTextView>()?.SetSpellUseSuppression(runtimeId);
            }
        }

        public void CancelPendingSpellCardUses()
        {
            for (var i = _pendingSpellUses.Count - 1; i >= 0; i--)
                if (!_pendingSpellUses[i].Accepted)
                { RemoveSpellUse(_pendingSpellUses[i].View); _pendingSpellUses.RemoveAt(i); }
            ApplySpellUseSuppressionToViews();
        }

        public void CancelSpellCardUses()
        {
            foreach (var pending in _pendingSpellUses)
                if (!pending.Accepted) RemoveSpellUse(pending.View);
            _pendingSpellUses.Clear();
            foreach (var view in _spellUseViews) RemoveSpellUse(view);
            _spellUseViews.Clear();
            ApplySpellUseSuppressionToViews();
        }

        private static void RemoveSpellUse(SpellCardUseVfx view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
    }
}
