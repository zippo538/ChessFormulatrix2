namespace ChessFormulatrix.WebAPI.DTO;

public class MoveHistoryDto
{
    public MoveDto Move { get; set; } = null!;
    public string PieceType { get; set; } = string.Empty;
    public string PieceColor { get; set; } = string.Empty;
    public string? CapturedPieceType { get; set; }   
}