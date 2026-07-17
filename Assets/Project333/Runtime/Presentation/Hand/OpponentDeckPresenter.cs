namespace Project333.Runtime.Presentation.Hand
{
    public sealed class OpponentDeckPresenter : PlayerDeckPresenter
    {
        protected override bool PresentsOpponentDeck => true;
        protected override string SafeAreaRootName => "OpponentDeckSafeArea";
        protected override string DeckRootName => "OpponentDeckRoot";
        protected override string CardBackObjectPrefix => "OpponentDeckCardBack_";
        protected override string TooltipSubject => "상대방 덱";
        protected override string GeneratedSpriteSuffix => "OpponentDeckRuntimeSprite";
    }
}
