using ChessConsole.Model;
using ChessConsole.Enums;
namespace Game.Tests;

[TestFixture]
public class GameEngineTest
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private ChessConsole2.Game _game = null!;

    [SetUp]
    public void Setup()
    {
        _whitePlayer = new Player("Mahindra", Color.White);
        _blackPlayer =  new Player("Saputra", Color.Black);
        
        _game = new ChessConsole2.Game(_whitePlayer, _blackPlayer,vsBot:true);
        _game.StartGame();
    }
        // ==========================================
        // 1. INITIALIZE BOARD TESTS
        // ==========================================
        [Test]
        public void Constructor_ShouldInitializeBoardWithCorrectPiecePositions()
        {
            var board = _game.GetCurrentBoard();

            // Cek Bidak Perwira Putih (Row 0)
            Assert.That(board.Tiles[0 * 8 + 0].ChessPiece?.Type, Is.EqualTo(PieceType.Rook));
            Assert.That(board.Tiles[0 * 8 + 1].ChessPiece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[0 * 8 + 2].ChessPiece?.Type, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[0 * 8 + 3].ChessPiece?.Type, Is.EqualTo(PieceType.Queen));
            Assert.That(board.Tiles[0 * 8 + 4].ChessPiece?.Type, Is.EqualTo(PieceType.King));
            Assert.That(board.Tiles[0 * 8 + 5].ChessPiece?.Type, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[0 * 8 + 6].ChessPiece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[0 * 8 + 7].ChessPiece?.Type, Is.EqualTo(PieceType.Rook));

            // Cek Pion Putih (Row 1) & Pion Hitam (Row 6)
            for (int col = 0; col < 8; col++)
            {
                var whitePawn = board.Tiles[1 * 8 + col].ChessPiece;
                Assert.IsNotNull(whitePawn);
                Assert.That(whitePawn!.Type, Is.EqualTo(PieceType.Pawn));
                Assert.That(whitePawn.PieceColor, Is.EqualTo(Color.White));

                var blackPawn = board.Tiles[6 * 8 + col].ChessPiece;
                Assert.IsNotNull(blackPawn);
                Assert.That(blackPawn!.Type, Is.EqualTo(PieceType.Pawn));
                Assert.That(blackPawn.PieceColor, Is.EqualTo(Color.Black));
            }

            // Cek Bidak Perwira Hitam (Row 7)
            Assert.That(board.Tiles[7 * 8 + 0].ChessPiece?.Type, Is.EqualTo(PieceType.Rook));
            Assert.That(board.Tiles[7 * 8 + 1].ChessPiece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[7 * 8 + 2].ChessPiece?.Type, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[7 * 8 + 3].ChessPiece?.Type, Is.EqualTo(PieceType.Queen));
            Assert.That(board.Tiles[7 * 8 + 4].ChessPiece?.Type, Is.EqualTo(PieceType.King));
            Assert.That(board.Tiles[7 * 8 + 5].ChessPiece?.Type, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[7 * 8 + 6].ChessPiece?.Type, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[7 * 8 + 7].ChessPiece?.Type, Is.EqualTo(PieceType.Rook));

            // Cek Baris Tengah (Row 2 sampai 5) Wajib Kosong (Null)
            for (int row = 2; row <= 5; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Assert.IsNull(board.Tiles[row * 8 + col].ChessPiece, $"Tile {row},{col} harus kosong");
                }
            }
        }
        // ==========================================
        // 2. START GAME TESTS
        // ==========================================
        [Test]
        public void StartGame_ShouldSetCurrentPlayerToWhiteAndInitializeTimers()
        {
            // Act
            _game.StartGame();

            // Assert
            Assert.That(_game.CurrentPlayer, Is.EqualTo(_whitePlayer), "Pemain pertama harus WhitePlayer.");
            Assert.IsNotNull(_game.WhiteTimer, "WhiteTimer harus terinisialisasi.");
            Assert.IsNotNull(_game.BlackTimer, "BlackTimer harus terinisialisasi.");

            var timeStatus = _game.CheckTimeStatus();
            Assert.IsFalse(timeStatus.IsExpired, "Waktu permainan baru mulai tidak boleh expired.");
        }
        // ==========================================
        // 3. RESIGN TESTS
        // ==========================================
        [Test]
        public void Resign_WhitePlayerResigns_ShouldSetWinnerToBlack()
        {
            // Arrange
            _game.StartGame();

            // Act: Putih menyerah
            _game.Resign(_whitePlayer);

            // Assert
            Assert.That(_game.GameResult.Type, Is.EqualTo(GameOverType.Resigned));
            Assert.That(_game.GameResult.Winner, Is.EqualTo(Color.Black), "Jika Putih menyerah, pemenang harus Hitam.");
        }

        [Test]
        public void Resign_BlackPlayerResigns_ShouldSetWinnerToWhite()
        {
            // Arrange
            _game.StartGame();

            // Act: Hitam menyerah
            _game.Resign(_blackPlayer);

            // Assert
            Assert.That(_game.GameResult.Type, Is.EqualTo(GameOverType.Resigned));
            Assert.That(_game.GameResult.Winner, Is.EqualTo(Color.White), "Jika Hitam menyerah, pemenang harus Putih.");
        }

        [Test]
        public void Resign_ShouldTriggerOnGameOverEvent()
        {
            // Arrange
            _game.StartGame();
            bool eventTriggered = false;
            _game.OnGameOver += () => eventTriggered = true;

            // Act
            _game.Resign(_whitePlayer);

            // Assert
            Assert.IsTrue(eventTriggered, "Event OnGameOver harus dipanggil saat pemain menyerah.");
        }
    
    
}