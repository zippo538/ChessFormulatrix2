using ChessConsole.Enums;
using ChessConsole.Interface;

namespace ChessConsole.Helper;

public class GameHelper
{
    public static string GetPieceSymbol(PieceType type, Color color)
    {
        return type switch
        {
            PieceType.Pawn => color == Color.White ? " ♙ " : " ♟ ",
            PieceType.Rook => color == Color.White ? " ♖ " : " ♜ ",
            PieceType.Knight => color == Color.White ? " ♘ " : " ♞ ",
            PieceType.Bishop => color == Color.White ? " ♗ " : " ♝ ",
            PieceType.Queen => color == Color.White ? " ♕ " : " ♛ ",
            PieceType.King => color == Color.White ? " ♔ " : " ♚ ",
            _ => "?"
        };
    }
    

    public static string GetPieceSymbol(IPiece piece)
    {
        if (piece == null) return "?";
        return GetPieceSymbol(piece.Piece, piece.PieceColor);
    }

    public static Color GetOpponent(Color color)
    {
        return color == Color.White ? Color.Black : Color.White;
    }
}