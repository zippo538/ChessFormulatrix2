using ChessConsole.Enums;
using Game.Tests.Helper;

namespace Game.Tests.GameEngine;


public class CheckTimeStatusTest
{
    [Test]
    public void CheckTimeStatus_WhenWhiteTimeIsZero_SetsTimeoutAndBlackAsWinner()
    {
        var game = CreateGameHelper.CreateStartedGame(clockEnabled: true);
        bool gameOverRaised = false;
        game.OnGameOver += () => gameOverRaised = true;

        CreateGameHelper.SetPrivateField(game, "_whiteRemaining", TimeSpan.Zero);

        var status = game.CheckTimeStatus();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsExpired, Is.True);
            Assert.That(status.TimedOutColor, Is.EqualTo(Color.White));
            Assert.That(status.WhiteRemaining, Is.EqualTo(TimeSpan.Zero));
            Assert.That(game.GameResult.Type, Is.EqualTo(GameOverType.Timeout));
            Assert.That(game.GameResult.Winner, Is.EqualTo(Color.Black));
            Assert.That(gameOverRaised, Is.True);
        });
    }
    
}