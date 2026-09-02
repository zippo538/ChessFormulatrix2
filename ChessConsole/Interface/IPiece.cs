using ChessConsole.Enums;

namespace ChessConsole.Interface;

public interface IPiece
{
    public Color PieceColor { get; }
    public PieceType Piece { get; }
    public bool HasMoved { get; set; }
}