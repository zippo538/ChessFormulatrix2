using ChessConsole;
using ChessConsole.Interface;
using ChessConsole.Enums;
using ChessConsole.Model;
using NUnit.Framework;
using Moq;

namespace Game.Tests.GameEngine;


public class GameTests
{
    [TestFixture]
    public class GameInterfaceTests
    {
        private class DummyGame : IGame
        {
        }


        [Test]
        public void DefaultInterface_GetCurrentTurn_ShouldReturnWhite()
        {
            IGame game = new DummyGame();

            Color currentTurn = game.GetCurrentTurn();

            Assert.That(currentTurn, Is.EqualTo(Color.White));
        }

        [Test]
        public void DefaultInterface_GetCurrentBoard_ShouldReturnNewBoardInstance()
        {
            IGame game = new DummyGame();

            Board board = game.GetCurrentBoard();

            Assert.IsNotNull(board);
            Assert.IsNotNull(board.Tiles);
        }

        [Test]
        public void DefaultInterface_GetLegalMoves_ShouldReturnEmptyList()
        {
            IGame game = new DummyGame();

            List<Move> legalMoves = game.GetLegalMoves();

            Assert.IsNotNull(legalMoves);
            Assert.That(legalMoves.Count, Is.EqualTo(0));
        }

        [Test]
        public void DefaultInterface_GetTimers_ShouldReturnOneSecondDefault()
        {
            IGame game = new DummyGame();

            TimeSpan whiteTimer = game.GetWhiteTimer();
            TimeSpan blackTimer = game.GetBlackTimer();

            Assert.That(whiteTimer, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(blackTimer, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void DefaultInterface_Resign_ShouldReturnNewGameOverResult()
        {
            IGame game = new DummyGame();

            GameOverResult result = game.Resign();

            Assert.IsNotNull(result);
            Assert.That(result.Type, Is.EqualTo(GameOverType.None));
        }


        [Test]
        public void MockIGame_StartGameAndMakeMove_ShouldExecuteWithoutException()
        {
            var mockGame = new Mock<IGame>();

            var sampleMove = new Move
            {
                From = new Postition { Row = 1, Column = 4 },
                To = new Postition { Row = 3, Column = 4 },
                Type = MoveType.Normal,
                PromotionType = PieceType.None
            };

            mockGame.Object.StartGame();
            mockGame.Object.MakeMove(sampleMove);

            mockGame.Verify(g => g.StartGame(), Times.Once);
            mockGame.Verify(g => g.MakeMove(sampleMove), Times.Once);
        }

        [Test]
        public void MockIGame_OverriddenMethods_ShouldReturnCustomValues()
        {
            var mockGame = new Mock<IGame>();

            mockGame.Setup(g => g.GetCurrentTurn()).Returns(Color.Black);
            mockGame.Setup(g => g.GetWhiteTimer()).Returns(TimeSpan.FromMinutes(5));
            mockGame.Setup(g => g.GetMoveResult()).Returns(MoveResult.Checked);

            Assert.That(mockGame.Object.GetCurrentTurn(), Is.EqualTo(Color.Black));
            Assert.That(mockGame.Object.GetWhiteTimer(), Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(mockGame.Object.GetMoveResult(), Is.EqualTo(MoveResult.Checked));
        }
    }
}