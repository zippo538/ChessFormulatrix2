using ChessConsole.Interface;
using ChessConsole.Enums;

namespace Game.Tests.GameEngine;

[TestFixture]
public class PlayerTest
{
    [Test]
    public void IPlayer_WhenCreated_ImplementsIPlayer()
    {
        IPlayer player = new DummyPlayer(
            "Pemain Putih",
            Color.White);

        Assert.Multiple(() =>
        {
            Assert.That(player, Is.InstanceOf<IPlayer>());
            Assert.That(player.Name, Is.EqualTo("Pemain Putih"));
            Assert.That(player.PlayerColor, Is.EqualTo(Color.White));
        });
    }

    [Test]
    public void IPlayer_NameCanBeChanged()
    {
        IPlayer player = new DummyPlayer(
            "Nama Lama",
            Color.White);

        player.Name = "Nama Baru";

        Assert.That(player.Name, Is.EqualTo("Nama Baru"));
    }
}

public sealed class DummyPlayer : IPlayer
{
    public string Name { get; set; }

    public Color PlayerColor { get; }

    public DummyPlayer(
        string name,
        Color playerColor)
    {
        Name = name;
        PlayerColor = playerColor;
    }
}