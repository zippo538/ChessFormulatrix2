using ChessFormularix.Console.Enums;
using ChessFormularix.Console.Model;

namespace ChessFormularix.Console.Interface;

public interface IGame
{
    void StartGame(){}
    void MakeMove(Move move) {}

    Color GetCurrentTurn() => Color.White;
    Board GetCurrentBoard() => new();
    List<Move> GetLegalMoves() => [];
    MoveResult GetMoveResult() => new();
    TimeSpan GetWhiteTimer() => TimeSpan.FromSeconds(1);
    TimeSpan GetBlackTimer() => TimeSpan.FromSeconds(1);
    GameOverResult Resign() => new();
    



}