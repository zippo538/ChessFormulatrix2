using ChessConsole.Enums;

namespace ChessWeb.DTO;

public class PieceDto
{
    public  Color PieceColor { get; } 
    public PieceType Type { get; set; } 
    public bool HasMoved { get; set; } 
}