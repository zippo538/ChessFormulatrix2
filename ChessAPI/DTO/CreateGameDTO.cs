namespace ChessWeb.DTO;

public class CreateGameDTO
{
    public string? WhiteName { get; set; }
    public string? BlackName { get; set; }
    public bool VsBot { get; set; } = true;

    // Kontrol waktu yang dipilih frontend.
    public int Minutes { get; set; } = 10;

    // false digunakan untuk mode latihan tanpa batas waktu.
    public bool ClockEnabled { get; set; } = true;
    
}