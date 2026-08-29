using ChessConsole2.Enums;
using ChessConsole2.Interface;

namespace ChessConsole2.Model;

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
