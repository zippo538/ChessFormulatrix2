using ChessConsole.Model;
using ChessConsole.Enums;

namespace Game.Tests.Helper;

public class PieceGameHelper
{
    public static void Move(ChessConsole2.Game game, int fromRow, int fromColumn, int toRow, int toColumn)
    {
        var from = new Postition { Row = fromRow, Column = fromColumn };
        var to = new Postition { Row = toRow, Column = toColumn };

        Assert.That(
            game.TrySelectPiece(from),
            Is.True,
            $"Bidak pada ({fromRow}, {fromColumn}) tidak dapat dipilih.");

        Assert.That(
            game.MakeMove(new Move
            {
                From = from,
                To = to,
                Type = MoveType.Normal,
                PromotionType = PieceType.None
            }),
            Is.True,
            $"Langkah ({fromRow}, {fromColumn}) ke ({toRow}, {toColumn}) ditolak.");
    }

     
    public static void Place(
        Board board,
        int row,
        int column,
        Color color,
        PieceType pieceType)
    {
        board.Tiles[row * 8 + column].ChessPiece = new Pieces(color, pieceType);
    }
}