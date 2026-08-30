namespace ChessWeb.DTO;

public class CreateGameDTO
{
    // Nama pemain Putih (selalu manusia, di kedua mode).
    public string WhiteName { get; set; } = string.Empty;
 
    // Nama pemain Hitam. Hanya dipakai saat VsBot = false (mode PvP lokal).
    // Boleh null/kosong saat VsBot = true, karena backend akan memakai
    // nama Bot bawaan ("AI Bot").
    public string? BlackName { get; set; }
 
    // true  = Hitam dikendalikan oleh engine Bot (Minimax + Alpha-Beta).
    // false = player vs player lokal, kedua sisi dikendalikan manusia.
    public bool VsBot { get; set; } = true;
}