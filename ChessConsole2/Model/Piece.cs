using ChessConsole2.Enums;
using ChessConsole2.Interface;

namespace ChessConsole2.Model;

public class Piece : IPiece
{
    public Color PieceColor { get; }
    public PieceType Type { get; }
    public bool Hasmove { get; set; }

    public Piece(Color pieceColor, PieceType type)
    {
        PieceColor = pieceColor;
        Type = type;
        Hasmove = false;
    }
}
