using ChessFormularix.Console.Enums;

namespace ChessFormularix.Console.Model;

public record struct Move
{
    public Postition From { get; set; }
    public Postition To { get; set; }
    public PieceType PromotionType { get; set; }
    public MoveType Type { get; set; }
    
}