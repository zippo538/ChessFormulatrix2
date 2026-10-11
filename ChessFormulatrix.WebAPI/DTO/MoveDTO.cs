namespace ChessFormulatrix.WebAPI.DTO;

public class MoveDto
{
    public PosititionDto From { get; set; } = null!;
    public PosititionDto To { get; set; } = null!;
    public string PromotionType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}