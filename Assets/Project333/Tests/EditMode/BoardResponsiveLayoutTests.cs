using NUnit.Framework;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Presentation.Battle;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class BoardResponsiveLayoutTests
    {
        [Test]
        public void Calculate_LandscapeScreen_FillsHeightAndKeepsBoardsOnTheirSides()
        {
            var layout = BoardResponsiveLayoutCalculator.Calculate(
                1920,
                1080,
                new Rect(0f, 0f, 1920f, 1080f),
                0.9f,
                24f,
                48f,
                new Vector2(8f, 8f),
                1f);

            Assert.That(layout.TileSize.x, Is.EqualTo(188f).Within(0.001f));
            Assert.That(layout.TileSize.y, Is.EqualTo(188f).Within(0.001f));

            var playerFrontTop = layout.GetTileCenter(PlayerId.Player, new TileCoord(0, 0));
            var playerBackTop = layout.GetTileCenter(PlayerId.Player, new TileCoord(0, 1));
            var aiFrontBottom = layout.GetTileCenter(PlayerId.AI, new TileCoord(0, 0));
            var aiBackBottom = layout.GetTileCenter(PlayerId.AI, new TileCoord(0, 1));

            Assert.That(playerFrontTop.x, Is.LessThan(0f));
            Assert.That(playerFrontTop.x, Is.GreaterThan(playerBackTop.x));
            Assert.That(aiFrontBottom.x, Is.GreaterThan(0f));
            Assert.That(aiFrontBottom.x, Is.LessThan(aiBackBottom.x));
            Assert.That(playerFrontTop.y, Is.EqualTo(-aiFrontBottom.y).Within(0.001f));
        }

        [Test]
        public void Calculate_PortraitScreen_UsesSideWidthAsSizeLimit()
        {
            var layout = BoardResponsiveLayoutCalculator.Calculate(
                720,
                1280,
                new Rect(0f, 0f, 720f, 1280f),
                0.9f,
                24f,
                48f,
                new Vector2(8f, 8f),
                1f);

            Assert.That(layout.TileSize, Is.EqualTo(new Vector2(152f, 152f)));

            var playerBack = layout.GetTileCenter(PlayerId.Player, new TileCoord(2, 1));
            var playerFront = layout.GetTileCenter(PlayerId.Player, new TileCoord(2, 0));
            Assert.That(playerBack.x - layout.TileSize.x * 0.5f, Is.GreaterThanOrEqualTo(-360f));
            Assert.That(playerFront.x + layout.TileSize.x * 0.5f, Is.LessThan(0f));
        }

        [Test]
        public void Calculate_NotchedSafeArea_CentersBothBoardsSymmetrically()
        {
            var layout = BoardResponsiveLayoutCalculator.Calculate(
                2400,
                1080,
                new Rect(100f, 0f, 2200f, 1080f),
                0.9f,
                24f,
                48f,
                new Vector2(8f, 8f),
                1f);

            Assert.That(layout.PlayerBoardCenter.x, Is.EqualTo(-layout.AIBoardCenter.x).Within(0.001f));
            Assert.That(layout.PlayerBoardCenter.y, Is.Zero.Within(0.001f));
            Assert.That(layout.AIBoardCenter.y, Is.Zero.Within(0.001f));
        }

        [Test]
        public void Calculate_OutwardOffset_MovesBoardsWithoutChangingTileSize()
        {
            var baseline = BoardResponsiveLayoutCalculator.Calculate(
                1920,
                1080,
                new Rect(0f, 0f, 1920f, 1080f),
                0.9f,
                24f,
                48f,
                new Vector2(8f, 8f),
                1f);
            var shifted = BoardResponsiveLayoutCalculator.Calculate(
                1920,
                1080,
                new Rect(0f, 0f, 1920f, 1080f),
                0.9f,
                24f,
                48f,
                new Vector2(8f, 8f),
                1f,
                32f);

            Assert.That(shifted.TileSize, Is.EqualTo(baseline.TileSize));
            Assert.That(shifted.PlayerBoardCenter.x, Is.EqualTo(baseline.PlayerBoardCenter.x - 32f));
            Assert.That(shifted.AIBoardCenter.x, Is.EqualTo(baseline.AIBoardCenter.x + 32f));
        }

        [Test]
        public void CalculateUniformTileScale_DoubledSquareTile_ReturnsTwo()
        {
            var scale = BoardResponsiveLayoutCalculator.CalculateUniformTileScale(
                new Vector2(160f, 160f),
                new Vector2(80f, 80f));

            Assert.That(scale, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void CalculateUniformTileScale_NonSquareTile_UsesSmallerAxis()
        {
            var scale = BoardResponsiveLayoutCalculator.CalculateUniformTileScale(
                new Vector2(160f, 120f),
                new Vector2(80f, 80f));

            Assert.That(scale, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void ScaleTileRelativeOffset_DoubledTile_DoublesOffset()
        {
            var scaledOffset = BoardResponsiveLayoutCalculator.ScaleTileRelativeOffset(
                new Vector2(0f, 50f),
                new Vector2(160f, 160f),
                new Vector2(80f, 80f));

            Assert.That(scaledOffset, Is.EqualTo(new Vector2(0f, 100f)));
        }
    }
}
