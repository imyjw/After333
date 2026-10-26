namespace Project333.Runtime.Presentation.Hand
{
    public sealed partial class HandCardTextView
    {
        private TemporaryReplicateHourglass _temporaryReplicateHourglass;

        private void RefreshTemporaryReplicateHourglass()
        {
            TemporaryReplicateHourglass.Present(ref _temporaryReplicateHourglass, TutorialArtwork,
                _handCardView != null && _handCardView.HasCard && _handCardView.IsTemporaryReplicate);
        }
    }
}
