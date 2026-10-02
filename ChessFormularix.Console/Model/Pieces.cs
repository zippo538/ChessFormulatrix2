using ChessConsole.Enums;
using ChessConsole.Interface;

namespace ChessConsole.Model;

public class Pieces : IPiece
{
    public Color PieceColor { get; }
    public PieceType Piece { get; }
    public bool HasMoved { get; set; }

    public Pieces(Color pieceColor, PieceType piece)
    {
        PieceColor = pieceColor;
        Piece = piece;
        HasMoved = false;
    }
}
