using ChessConsole.Enums;

namespace ChessConsole.Model;

public class MoveHistory
{
    public Move Move { get; }
    public PieceType PieceType { get; }
    public Color PieceColor { get; }
    public PieceType? CapturedPieceType { get; }

    public MoveHistory(
        Move move,
        PieceType pieceType,
        Color pieceColor,
        PieceType? capturedPieceType)
    {
        Move = move;
        PieceType = pieceType;
        PieceColor = pieceColor;
        CapturedPieceType = capturedPieceType;
    }
}