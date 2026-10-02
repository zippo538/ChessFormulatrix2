using ChessConsole.Enums;
namespace ChessWeb.DTO;

public class PieceDto
{
    public string PieceColor { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool HasMoved { get; set; } 
}