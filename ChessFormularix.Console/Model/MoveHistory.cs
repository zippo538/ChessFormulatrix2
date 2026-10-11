using ChessFormularix.Console.Enums;

namespace ChessFormularix.Console.Model;

public class MoveHistory(
    Move move,
    PieceType pieceType,
    Color pieceColor,
    PieceType? capturedPieceType)
{
    public Move Move { get; } = move;
    public PieceType PieceType { get; } = pieceType;
    public Color PieceColor { get; } = pieceColor;
    public PieceType? CapturedPieceType { get; } = capturedPieceType;
}