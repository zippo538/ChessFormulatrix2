using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormulatrix.Tests.GameEngine;

[TestFixture]
public class GameTimerTest
{
    private Player _whitePlayer = null!;
        private Player _blackPlayer = null!;
        private ChessFormularix.Console.Game _game = null!;

        [SetUp]
        public void Setup()
        {
            _whitePlayer = new Player("Putih", Color.White);
            _blackPlayer = new Player("Hitam", Color.Black);
        }

        [Test]
        public void StartGame_ShouldInitializeTimerWithConfiguredMinutes()
        {
            _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 5, clockEnabled: true);

            _game.StartGame();
            var timeStatus = _game.CheckTimeStatus();

            Assert.That(timeStatus.WhiteRemaining, Is.EqualTo(TimeSpan.FromMinutes(5)).Within(1).Seconds);
            Assert.That(timeStatus.BlackRemaining, Is.EqualTo(TimeSpan.FromMinutes(5)).Within(1).Seconds);
            Assert.IsFalse(timeStatus.IsExpired);
            Assert.IsNull(timeStatus.TimedOutColor);
        }

        [Test]
        public void CheckTimeStatus_WhenClockDisabled_ShouldNotDeductTime()
        {
            _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 10, clockEnabled: false);
            _game.StartGame();

            Thread.Sleep(100);
            var timeStatus = _game.CheckTimeStatus();

            Assert.That(timeStatus.WhiteRemaining, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.IsFalse(timeStatus.IsExpired);
        }

        [Test]
        public void MakeMove_ShouldDeductActivePlayerTimeAfterTurnSwitch()
        {
            _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 10, clockEnabled: true);
            _game.StartGame();

            Thread.Sleep(150);

            var fromPos = new Postition { Row = 1, Column = 4 };
            var toPos = new Postition { Row = 3, Column = 4 };
    
            _game.TrySelectPiece(fromPos);
            bool moveSuccess = _game.MakeMove(new Move
            {
                From = fromPos,
                To = toPos,
                Type = MoveType.Normal,
                PromotionType = PieceType.None
            });

            Assert.IsTrue(moveSuccess);

            var timeStatus = _game.CheckTimeStatus();

            Assert.Less(timeStatus.WhiteRemaining, TimeSpan.FromMinutes(10));
    
            Assert.That(timeStatus.BlackRemaining, Is.EqualTo(TimeSpan.FromMinutes(10)).Within(1).Seconds);
    
            Assert.That(_game.CurrentPlayer.PlayerColor, Is.EqualTo(Color.Black));
        }

        [Test]
        public void DeclareTimeout_WhenWhiteTimeOut_ShouldSetWinnerToBlackAndEndGame()
        {
            _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 1, clockEnabled: true);
            _game.StartGame();

            _game.OnGameOver += () => _ = true;

            _game.WhiteTimer.StopTimer(); 
            
            _game.Resign(_whitePlayer); 

            Assert.IsNotNull(_game.WhiteTimer);
            Assert.IsNotNull(_game.BlackTimer);
        }

        [Test]
        public void CheckTimeStatus_WhenTimeExpired_ShouldDeclareTimeoutCorrectly()
        {
            _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 1, clockEnabled: true);
            _game.StartGame();

            var initialStatus = _game.CheckTimeStatus();

            Assert.IsFalse(initialStatus.IsExpired);
            Assert.Greater(initialStatus.WhiteRemaining, TimeSpan.Zero);
            Assert.Greater(initialStatus.BlackRemaining, TimeSpan.Zero);
        }
}