using ChessFormularix.Console.Enums;

namespace ChessFormularix.Console.Interface;

public interface IPiece
{
    public Color PieceColor { get; }
    public PieceType Piece { get; }
    public bool HasMoved { get; set; }
}