using ChessConsole.Enums;

namespace ChessWeb.DTO;

public class MoveDTO
{
    public PosititionDTO From { get; set; } = null!;
    public PosititionDTO To { get; set; } = null!;
    public PieceType? PromotionType { get; set; } 
    public MoveType? Type { get; set; } 
}