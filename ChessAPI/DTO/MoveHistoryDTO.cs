namespace ChessWeb.DTO;

public class MoveHistoryDTO
{
    public MoveDTO Move { get; set; } = null!;
    public string PieceType { get; set; } = string.Empty;
    public string PieceColor { get; set; } = string.Empty;
    public string? CapturedPieceType { get; set; }   
}