using ChessConsole2.Enums;

namespace ChessConsole2.Interface;

public interface IPlayer
{ 
     string Name { get; set; }
     Color PlayerColor { get; }
}