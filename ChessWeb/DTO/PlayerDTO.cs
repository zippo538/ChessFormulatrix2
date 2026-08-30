using ChessConsole.Enums;
using System.Text.Json.Serialization;
namespace ChessWeb.DTO;

public class PlayerDTO
{
    public string Name { get; set; } = string.Empty;
    public Color PlayerColor { get; set; } 
}