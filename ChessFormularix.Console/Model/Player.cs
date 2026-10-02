using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Interface;

namespace ChessFormularix.Console.Model;

public class Player(string name, Color color) : IPlayer
{
    public string Name { get; set; } = name;
    public Color PlayerColor { get; } = color;
}
