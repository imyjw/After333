using NUnit.Framework;
using Project333.Runtime.Presentation.Battle;

namespace Project333.Tests.EditMode
{
    public sealed class BoardPresenterLayoutTests
    {
        [Test]
        public void GetCoordForIndex_MapsTenTilesInColumnThenRowOrder()
        {
            Assert.That(BoardPresenterLayout.TilesPerSide, Is.EqualTo(10));

            Assert.That(BoardPresenterLayout.GetCoordForIndex(0).ToString(), Is.EqualTo("(0,0)"));
            Assert.That(BoardPresenterLayout.GetCoordForIndex(1).ToString(), Is.EqualTo("(0,1)"));
            Assert.That(BoardPresenterLayout.GetCoordForIndex(2).ToString(), Is.EqualTo("(1,0)"));
            Assert.That(BoardPresenterLayout.GetCoordForIndex(3).ToString(), Is.EqualTo("(1,1)"));
            Assert.That(BoardPresenterLayout.GetCoordForIndex(8).ToString(), Is.EqualTo("(4,0)"));
            Assert.That(BoardPresenterLayout.GetCoordForIndex(9).ToString(), Is.EqualTo("(4,1)"));
        }
    }
}
