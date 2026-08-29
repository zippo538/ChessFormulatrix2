using ChessConsole2.Enums;
using ChessConsole2.Model;

namespace ChessConsole2.Interface;

public interface IGame
{
    void StartGame(){}
    void MakeMove(Move move) {}

    Color GetCurrentTurn() => Color.White;
    Board GetCurrentBoard() => new();
    List<Move> GetLegalMoves() => new();
    MoveResult GetMoveResult() => new();
    TimeSpan GetWhiteTimer() => TimeSpan.FromSeconds(1);
    TimeSpan GetBlackTimer() => TimeSpan.FromSeconds(1);
    GameOverResult Resign() => new();
    



}