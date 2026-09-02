namespace ChessWeb.DTO;

public class CreateGameDTO
{
    public string? WhiteName { get; set; }
    public string? BlackName { get; set; }
    public bool VsBot { get; set; } = true;

    public int Minutes { get; set; } = 10;

    public bool ClockEnabled { get; set; } = true;
    
}