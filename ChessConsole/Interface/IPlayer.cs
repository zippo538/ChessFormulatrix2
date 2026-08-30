using ChessConsole.Enums;

namespace ChessConsole.Interface;

public interface IPlayer
{ 
     string Name { get; set; }
     Color PlayerColor { get; }
}