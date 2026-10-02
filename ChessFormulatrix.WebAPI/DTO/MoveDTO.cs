namespace ChessWeb.DTO;

public class MoveDTO
{
    public PosititionDTO From { get; set; } = null!;
    public PosititionDTO To { get; set; } = null!;
    public string PromotionType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}