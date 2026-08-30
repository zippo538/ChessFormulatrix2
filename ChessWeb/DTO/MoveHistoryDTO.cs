using ChessConsole.Enums;

namespace ChessWeb.DTO;

public class MoveHistoryDTO
{
    public MoveDTO Move { get; set; } = null!;
    public PieceType? PieceType { get; set; }
    public Color PieceColor { get; set; }
    public PieceType? CapturedPieceType { get; set; }   
}