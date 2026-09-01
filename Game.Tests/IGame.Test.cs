using ChessConsole;
using ChessConsole.Interface;
using ChessConsole.Enums;
using ChessConsole.Model;
using NUnit.Framework;
using Moq;

namespace Game.Tests;


public class IGame_Tests
{
    [TestFixture]
    public class IGameInterfaceTests
    {
        // Dummy implementation sederhana untuk mengecek default interface methods
        private class DummyGame : IGame
        {
            // Tidak meng-override default implementation
        }

        // ==========================================
        // 1. TESTING DEFAULT INTERFACE IMPLEMENTATIONS
        // ==========================================

        [Test]
        public void DefaultInterface_GetCurrentTurn_ShouldReturnWhite()
        {
            IGame game = new DummyGame();

            Color currentTurn = game.GetCurrentTurn();

            Assert.AreEqual(Color.White, currentTurn);
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
            Assert.AreEqual(0, legalMoves.Count);
        }

        [Test]
        public void DefaultInterface_GetTimers_ShouldReturnOneSecondDefault()
        {
            IGame game = new DummyGame();

            TimeSpan whiteTimer = game.GetWhiteTimer();
            TimeSpan blackTimer = game.GetBlackTimer();

            Assert.AreEqual(TimeSpan.FromSeconds(1), whiteTimer);
            Assert.AreEqual(TimeSpan.FromSeconds(1), blackTimer);
        }

        [Test]
        public void DefaultInterface_Resign_ShouldReturnNewGameOverResult()
        {
            IGame game = new DummyGame();

            GameOverResult result = game.Resign();

            Assert.IsNotNull(result);
            Assert.AreEqual(GameOverType.None, result.Type);
        }

        // ==========================================
        // 2. TESTING MOCKING CONTRACT (USING MCQ)
        // ==========================================

        [Test]
        public void MockIGame_StartGameAndMakeMove_ShouldExecuteWithoutException()
        {
            // Arrange
            var mockGame = new Mock<IGame>();

            var sampleMove = new Move
            {
                From = new Postition { Row = 1, Column = 4 },
                To = new Postition { Row = 3, Column = 4 },
                Type = MoveType.Normal,
                PromotionType = PieceType.None
            };

            // Act
            mockGame.Object.StartGame();
            mockGame.Object.MakeMove(sampleMove);

            // Assert: Memastikan bahwa metode void berhasil dipanggil persis 1 kali
            mockGame.Verify(g => g.StartGame(), Times.Once);
            mockGame.Verify(g => g.MakeMove(sampleMove), Times.Once);
        }

        [Test]
        public void MockIGame_OverriddenMethods_ShouldReturnCustomValues()
        {
            // Arrange
            var mockGame = new Mock<IGame>();

            mockGame.Setup(g => g.GetCurrentTurn()).Returns(Color.Black);
            mockGame.Setup(g => g.GetWhiteTimer()).Returns(TimeSpan.FromMinutes(5));
            mockGame.Setup(g => g.GetMoveResult()).Returns(MoveResult.Checked);

            // Act & Assert
            Assert.AreEqual(Color.Black, mockGame.Object.GetCurrentTurn());
            Assert.AreEqual(TimeSpan.FromMinutes(5), mockGame.Object.GetWhiteTimer());
            Assert.AreEqual(MoveResult.Checked, mockGame.Object.GetMoveResult());
        }
    }
}