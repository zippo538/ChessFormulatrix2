using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Interface;

namespace ChessFormularix.Console.Model;

public class Pieces(Color pieceColor, PieceType piece) : IPiece
{
    public Color PieceColor { get; } = pieceColor;
    public PieceType Piece { get; } = piece;
    public bool HasMoved { get; set; } = false;
}
