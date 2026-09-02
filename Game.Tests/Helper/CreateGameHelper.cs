using System.Reflection;
using System.Runtime.Serialization;
using ChessConsole.Enums;
using ChessConsole.Model;

namespace Game.Tests.Helper;

public class CreateGameHelper
{
    public static ChessConsole2.Game CreateStartedGame(bool clockEnabled)
    {
        var white = CreatePlayer(Color.White);
        var black = CreatePlayer(Color.Black);
        var game = new ChessConsole2.Game(
            white,
            black,
            vsBot: false,
            initialMinutes: 10,
            clockEnabled: clockEnabled);

        game.StartGame();
        return game;
    }
    public static void SetPrivateField<T>(ChessConsole2.Game game, string fieldName, T value)
    {
        var field = typeof(ChessConsole2.Game).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Field private {fieldName} tidak ditemukan.");
        field!.SetValue(game, value);
    }

    private static Player CreatePlayer(Color color)
    {
        foreach (var constructor in typeof(Player)
                     .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderBy(candidate => candidate.GetParameters().Length))
        {
            try
            {
                var arguments = constructor.GetParameters()
                    .Select(parameter => CreateArgument(parameter.ParameterType, color))
                    .ToArray();
                var player = (Player)constructor.Invoke(arguments);
                SetColorMember(player, color);
                return player;
            }
            catch
            {
                // Coba konstruktor lain; fallback di bawah menangani model
                // yang tidak menyediakan konstruktor yang dapat dipanggil.
            }
        }
        #pragma warning disable SYSLIB0050
        var uninitialized = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        #pragma warning restore SYSLIB0050
        SetColorMember(uninitialized, color);
        return uninitialized;
    }
    private static object? CreateArgument(Type type, Color color)
    {
        if (type == typeof(Color)) return color;
        if (type == typeof(string)) return color.ToString();
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }

    private static void SetColorMember(Player player, Color color)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var property = typeof(Player).GetProperty("PlayerColor", flags);
        if (property?.SetMethod != null)
        {
            property.SetValue(player, color);
            return;
        }

        var field = typeof(Player).GetField("PlayerColor", flags)
                    ?? typeof(Player).GetField("_playerColor", flags)
                    ?? typeof(Player).GetField("<PlayerColor>k__BackingField", flags);

        if (field == null)
            throw new InvalidOperationException("Member Player.PlayerColor tidak ditemukan.");

        field.SetValue(player, color);
    }
}