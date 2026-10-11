using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormulatrix.Tests.GameEngine;

[TestFixture]
public class CheckMoveTest
{
    private Player _whitePlayer = null!;
    private Player _blackPlayer = null!;
    private ChessFormularix.Console.Game _game = null!;
    private int _gameOverEventCount;


    [SetUp]
    public void Setup()
    {
        _whitePlayer = new Player("Mahindra", Color.White);
        _blackPlayer =  new Player("Saputra", Color.Black);
        
        _game = new ChessFormularix.Console.Game(_whitePlayer, _blackPlayer,vsBot:true);
        _game.StartGame();
        
        _gameOverEventCount = 0;
        _game.OnGameOver += () => _gameOverEventCount++;
        ClearBoard();
    }   
    [Test]
    public void CheckMoveResult_WhenOpponentIsCheckmated_SetsCheckmateResult()
    {
        PlacePiece(7, 7, Color.Black, PieceType.King);
        PlacePiece(6, 6, Color.White, PieceType.Queen);
        PlacePiece(5, 5, Color.White, PieceType.King);

        _game.CheckMoveResult();

        Assert.Multiple(() =>
        {
            Assert.That(
                _game.GameResult.Type,
                Is.EqualTo(GameOverType.Checkmate));

            Assert.That(
                _game.GameResult.Winner,
                Is.EqualTo(Color.White));

            Assert.That(
                _gameOverEventCount,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckMoveResult_WhenOpponentIsStalemated_SetsStalemateResult()
    {
        /*
         * Posisi:
         * Black King : h8
         * White Queen: f7
         * White King : f6
         *
         * Raja hitam tidak sedang skak, tetapi tidak memiliki langkah legal.
         */
        PlacePiece(7, 7, Color.Black, PieceType.King);
        PlacePiece(6, 5, Color.White, PieceType.Queen);
        PlacePiece(5, 5, Color.White, PieceType.King);

        _game.CheckMoveResult();

        Assert.Multiple(() =>
        {
            Assert.That(
                _game.GameResult.Type,
                Is.EqualTo(GameOverType.Stalemate));

            Assert.That(
                _gameOverEventCount,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void CheckMoveResult_WhenOpponentIsCheckedButCanEscape_DoesNotEndGame()
    {
        /*
         * Posisi:
         * Black King: h8
         * White Rook: h1
         * White King: a1
         *
         * Raja hitam sedang skak, tetapi masih dapat bergerak ke g8 atau g7.
         */
        PlacePiece(7, 7, Color.Black, PieceType.King);
        PlacePiece(0, 7, Color.White, PieceType.Rook);
        PlacePiece(0, 0, Color.White, PieceType.King);

        _game.CheckMoveResult();

        Assert.Multiple(() =>
        {
            Assert.That(
                _game.GameResult.Type,
                Is.EqualTo(GameOverType.None));

            Assert.That(
                _gameOverEventCount,
                Is.Zero);
        });
    }

    [Test]
    public void CheckMoveResult_WhenOpponentIsNotCheckedAndHasLegalMoves_DoesNotEndGame()
    {
        /*
         * Posisi normal:
         * Raja hitam tidak sedang skak dan masih memiliki langkah legal.
         */
        PlacePiece(7, 7, Color.Black, PieceType.King);
        PlacePiece(0, 0, Color.White, PieceType.King);
        PlacePiece(0, 1, Color.White, PieceType.Rook);

        _game.CheckMoveResult();

        Assert.Multiple(() =>
        {
            Assert.That(
                _game.GameResult.Type,
                Is.EqualTo(GameOverType.None));

            Assert.That(
                _gameOverEventCount,
                Is.Zero);
        });
    }

    private void ClearBoard()
    {
        var board = _game.GetCurrentBoard();

        foreach (var tile in board.Tiles)
        {
            tile.ChessPiece = null;
        }
    }

    private void PlacePiece(
        int row,
        int column,
        Color color,
        PieceType pieceType)
    {
        var board = _game.GetCurrentBoard();

        board.Tiles[row * 8 + column].ChessPiece =
            new Pieces(color, pieceType);
    }
}