using ChessConsole.Enums;
using ChessConsole.Interface;

namespace ChessConsole.Model;

public class Player : IPlayer
{
    public string Name { get; set; }
    public Color PlayerColor { get; }

    public Player(string name, Color color)
    {
        Name = name;
        PlayerColor = color;
    }
}
