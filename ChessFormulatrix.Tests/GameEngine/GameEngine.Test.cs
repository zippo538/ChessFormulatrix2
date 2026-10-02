using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormulatrix.Tests.GameEngine;

[TestFixture]
public class GameEngineTest
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private ChessFormularix.Console.Game _game = null!;

    [SetUp]
    public void Setup()
    {
        _whitePlayer = new Player("Mahindra", Color.White);
        _blackPlayer =  new Player("Saputra", Color.Black);
        
        _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer,vsBot:true);
        _game.StartGame();
    }
        [Test]
        public void Constructor_ShouldInitializeBoardWithCorrectPiecePositions()
        {
            var board = _game.GetCurrentBoard();

            Assert.That(board.Tiles[0 * 8 + 0].ChessPiece?.Piece, Is.EqualTo(PieceType.Rook));
            Assert.That(board.Tiles[0 * 8 + 1].ChessPiece?.Piece, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[0 * 8 + 2].ChessPiece?.Piece, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[0 * 8 + 3].ChessPiece?.Piece, Is.EqualTo(PieceType.Queen));
            Assert.That(board.Tiles[0 * 8 + 4].ChessPiece?.Piece, Is.EqualTo(PieceType.King));
            Assert.That(board.Tiles[0 * 8 + 5].ChessPiece?.Piece, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[0 * 8 + 6].ChessPiece?.Piece, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[0 * 8 + 7].ChessPiece?.Piece, Is.EqualTo(PieceType.Rook));

            for (int col = 0; col < 8; col++)
            {
                var whitePawn = board.Tiles[1 * 8 + col].ChessPiece;
                Assert.IsNotNull(whitePawn);
                Assert.That(whitePawn!.Piece, Is.EqualTo(PieceType.Pawn));
                Assert.That(whitePawn.PieceColor, Is.EqualTo(Color.White));

                var blackPawn = board.Tiles[6 * 8 + col].ChessPiece;
                Assert.IsNotNull(blackPawn);
                Assert.That(blackPawn!.Piece, Is.EqualTo(PieceType.Pawn));
                Assert.That(blackPawn.PieceColor, Is.EqualTo(Color.Black));
            }

            Assert.That(board.Tiles[7 * 8 + 0].ChessPiece?.Piece, Is.EqualTo(PieceType.Rook));
            Assert.That(board.Tiles[7 * 8 + 1].ChessPiece?.Piece, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[7 * 8 + 2].ChessPiece?.Piece, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[7 * 8 + 3].ChessPiece?.Piece, Is.EqualTo(PieceType.Queen));
            Assert.That(board.Tiles[7 * 8 + 4].ChessPiece?.Piece, Is.EqualTo(PieceType.King));
            Assert.That(board.Tiles[7 * 8 + 5].ChessPiece?.Piece, Is.EqualTo(PieceType.Bishop));
            Assert.That(board.Tiles[7 * 8 + 6].ChessPiece?.Piece, Is.EqualTo(PieceType.Knight));
            Assert.That(board.Tiles[7 * 8 + 7].ChessPiece?.Piece, Is.EqualTo(PieceType.Rook));

            for (int row = 2; row <= 5; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Assert.IsNull(board.Tiles[row * 8 + col].ChessPiece, $"Tile {row},{col} harus kosong");
                }
            }
        }
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
        [Test]
        public void Resign_WhitePlayerResigns_ShouldSetWinnerToBlack()
        {
            _game.StartGame();

            _game.Resign(_whitePlayer);

            Assert.That(_game.GameResult.Type, Is.EqualTo(GameOverType.Resigned));
            Assert.That(_game.GameResult.Winner, Is.EqualTo(Color.Black), "Jika Putih menyerah, pemenang harus Hitam.");
        }

        [Test]
        public void Resign_BlackPlayerResigns_ShouldSetWinnerToWhite()
        {
            _game.StartGame();

            _game.Resign(_blackPlayer);

            // Assert
            Assert.That(_game.GameResult.Type, Is.EqualTo(GameOverType.Resigned));
            Assert.That(_game.GameResult.Winner, Is.EqualTo(Color.White), "Jika Hitam menyerah, pemenang harus Putih.");
        }

        [Test]
        public void Resign_ShouldTriggerOnGameOverEvent()
        {
            _game.StartGame();
            bool eventTriggered = false;
            _game.OnGameOver += () => eventTriggered = true;

            _game.Resign(_whitePlayer);

            Assert.IsTrue(eventTriggered, "Event OnGameOver harus dipanggil saat pemain menyerah.");
        }
    
    
}