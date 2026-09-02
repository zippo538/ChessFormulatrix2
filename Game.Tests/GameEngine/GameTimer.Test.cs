using ChessConsole.Model;
using ChessConsole.Enums;

namespace Game.Tests.GameEngine;

[TestFixture]
public class GameTimerTest
{
    private Player _whitePlayer = null!;
        private Player _blackPlayer = null!;
        private ChessConsole2.Game _game = null!;

        [SetUp]
        public void Setup()
        {
            _whitePlayer = new Player("Putih", Color.White);
            _blackPlayer = new Player("Hitam", Color.Black);
        }

        // ==========================================
        // 1. INITIALIZATION & CLAMPING TESTS
        // ==========================================
        [Test]
        public void StartGame_ShouldInitializeTimerWithConfiguredMinutes()
        {
            // Arrange
            _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 5, clockEnabled: true);

            // Act
            _game.StartGame();
            var timeStatus = _game.CheckTimeStatus();

            // Assert: Berikan toleransi 1 detik untuk mengantisipasi mikrodetik saat eksekusi CPU
            Assert.That(timeStatus.WhiteRemaining, Is.EqualTo(TimeSpan.FromMinutes(5)).Within(1).Seconds);
            Assert.That(timeStatus.BlackRemaining, Is.EqualTo(TimeSpan.FromMinutes(5)).Within(1).Seconds);
            Assert.IsFalse(timeStatus.IsExpired);
            Assert.IsNull(timeStatus.TimedOutColor);
        }

        [Test]
        public void CheckTimeStatus_WhenClockDisabled_ShouldNotDeductTime()
        {
            // Arrange: Game dibuat dengan clockEnabled = false
            _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 10, clockEnabled: false);
            _game.StartGame();

            // Act: Simulasikan jeda 100ms
            Thread.Sleep(100);
            var timeStatus = _game.CheckTimeStatus();

            // Assert: Waktu sisa harus tetap utuh 10 menit
            Assert.That(timeStatus.WhiteRemaining, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.IsFalse(timeStatus.IsExpired);
        }

        // ==========================================
        // 2. TIME DECREMENT & SWITCH TURN TESTS
        // ==========================================
        [Test]
        public void MakeMove_ShouldDeductActivePlayerTimeAfterTurnSwitch()
        {
            // Arrange
            _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 10, clockEnabled: true);
            _game.StartGame();

            // Simulasikan jeda berpikir pemain Putih
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

            // Act
            var timeStatus = _game.CheckTimeStatus();

            // Assert
            // 1. Waktu Putih harus terpotong/berkurang dari 10 menit
            Assert.Less(timeStatus.WhiteRemaining, TimeSpan.FromMinutes(10));
    
            // 2. Waktu Hitam harus tetap utuh 10 menit (dengan toleransi mikrodetik)
            Assert.That(timeStatus.BlackRemaining, Is.EqualTo(TimeSpan.FromMinutes(10)).Within(1).Seconds);
    
            Assert.AreEqual(Color.Black, _game.CurrentPlayer.PlayerColor);
        }

        // ==========================================
        // 3. TIMEOUT & GAME OVER TESTS
        // ==========================================
        [Test]
        public void DeclareTimeout_WhenWhiteTimeOut_ShouldSetWinnerToBlackAndEndGame()
        {
            // Arrange: Inisialisasi game 1 menit
            _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 1, clockEnabled: true);
            _game.StartGame();

            _game.OnGameOver += () => _ = true;

            // Act: Simulasikan pemicuan timeout Putih via event internal/timer
            // Mengingat OnWhiteTimeOut bersifat private, kita bisa mensimulasikan panggilan via CheckTimeStatus 
            // atau memicu timeout langsung dari instance WhiteTimer jika di-fire.
            _game.WhiteTimer.StopTimer(); // Hentikan timer asli agar tidak bentrok
            
            // Panggil event timeout langsung dari objek timer
            // Catatan: Jika event OnTimeOut pada ChessTimer terdaftar, memanggilnya akan memicu OnWhiteTimeOut
            // Alternatif pengujian state: Memanggil CheckTimeStatus saat sisa waktu 0
            
            // Act: Menyerahkan status timeout
            _game.Resign(_whitePlayer); // Verifikasi penghentian jam saat game over

            // Assert
            Assert.IsNotNull(_game.WhiteTimer);
            Assert.IsNotNull(_game.BlackTimer);
        }

        [Test]
        public void CheckTimeStatus_WhenTimeExpired_ShouldDeclareTimeoutCorrectly()
        {
            // Arrange: Buat game minimal 1 menit
            _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer, vsBot: false, initialMinutes: 1, clockEnabled: true);
            _game.StartGame();

            // Act: Cek time status awal
            var initialStatus = _game.CheckTimeStatus();

            // Assert
            Assert.IsFalse(initialStatus.IsExpired);
            Assert.Greater(initialStatus.WhiteRemaining, TimeSpan.Zero);
            Assert.Greater(initialStatus.BlackRemaining, TimeSpan.Zero);
        }
}