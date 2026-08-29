using ChessConsole2.Enums;

namespace ChessConsole2.Interface;

public class IPiece
{
    public Color PieceColor { get; }
    public PieceType Piece { get; }
    public bool HasMoved { get; set; }
}