using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using UnityEngine;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandPresenter
    {
        private readonly HashSet<string> _pendingTemporaryExpiryIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TemporaryReplicateDissolveView> _temporaryExpiryViews = new List<TemporaryReplicateDissolveView>();

        // Called only for an accepted own-turn end, never inferred from a snapshot.
        public void QueueTemporaryReplicateExpiry()
        {
            foreach (var view in _handCardViews)
                if (view != null && view.HasCard && view.IsTemporaryReplicate && !string.IsNullOrEmpty(view.RuntimeId))
                    _pendingTemporaryExpiryIds.Add(view.RuntimeId);
        }

        private void PresentTemporaryReplicateExpiry(HandState nextHand)
        {
            if (nextHand == null) { CancelTemporaryReplicateExpiry(); return; }
            _temporaryExpiryViews.RemoveAll(view => view == null);
            if (_pendingTemporaryExpiryIds.Count == 0) return;
            if (UnityEngine.Application.isPlaying && isActiveAndEnabled)
                foreach (var previous in _handCardViews)
                {
                    if (previous == null || !previous.HasCard || !previous.IsTemporaryReplicate ||
                        !_pendingTemporaryExpiryIds.Contains(previous.RuntimeId)) continue;
                    var stillPresent = false;
                    if (nextHand != null)
                        foreach (var card in nextHand.Cards)
                            if (string.Equals(card.RuntimeId, previous.RuntimeId, StringComparison.Ordinal))
                            { stillPresent = true; break; }
                    if (stillPresent) continue;
                    _pendingTemporaryExpiryIds.Remove(previous.RuntimeId);
                    var artwork = previous.GetComponent<HandCardTextView>()?.TutorialArtwork;
                    var effect = TemporaryReplicateDissolveView.Play(artwork);
                    if (effect != null) _temporaryExpiryViews.Add(effect);
                }
            // A value-popup animation may refresh the old state before the final
            // StateView arrives. Keep still-present IDs pending through that refresh.
        }

        public void CancelTemporaryReplicateExpiry()
        {
            _pendingTemporaryExpiryIds.Clear();
            foreach (var view in _temporaryExpiryViews)
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    if (UnityEngine.Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            _temporaryExpiryViews.Clear();
        }

        private void OnDisable()
        {
            CancelTemporaryReplicateExpiry();
            CancelSpellCardUses();
            CancelCardDraws();
        }
    }
}
