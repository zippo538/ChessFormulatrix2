using Spectre.Console;
using Spectre.Console.Rendering;
using ChessConsole.Enums;
using ChessConsole.Model;
using ChessConsole2;
using Serilog;
using Serilog.Events;
using Color = ChessConsole.Enums.Color;

namespace ChessConsole;

public class Program
{
    public static void Main()
    {
        ConfigureLogging();
        try
        {
            Log.Information("Chess Engine application started");

            RunGame();
        }
        catch (Exception exception)
        {
            Log.Fatal(
                exception,
                "Chess Engine stopped because of an unhandled exception");

            AnsiConsole.MarkupLine(
                "\n[bold red]Terjadi kesalahan pada aplikasi.[/]");

            AnsiConsole.WriteException(
                exception,
                ExceptionFormats.ShortenEverything);
        }
        finally
        {
            Log.Information("Chess Console application stopped");

            Log.CloseAndFlush();
        }
    }
     private static void RunGame()
    {
        Console.OutputEncoding =
            System.Text.Encoding.UTF8;

        ShowTitle();

        var whitePlayer = new Player(
            AskPlayerName(
                "Nama pemain Putih",
                "Pemain Putih"),
            Color.White);

        var blackPlayer = new Player(
            AskPlayerName(
                "Nama pemain Hitam",
                "Pemain Hitam"),
            Color.Black);

        var gameId = Guid.NewGuid().ToString();

        var gameLogger = Log.Logger
            .ForContext<Game>()
            .ForContext("GameId", gameId);

        var game = new Game(
            whitePlayer,
            blackPlayer,
            vsBot: false,
            initialMinutes: 10,
            clockEnabled: true,
            logger: gameLogger,
            gameId: gameId);

        var gameOver = false;

        game.OnGameOver += () =>
        {
            gameOver = true;
        };

        Log.Information(
            "New chess session created. GameId: {GameId}, " +
            "WhitePlayer: {WhitePlayer}, BlackPlayer: {BlackPlayer}",
            gameId,
            whitePlayer.Name,
            blackPlayer.Name);

        game.StartGame();

        game.WhiteTimer.OnTimerTick +=
            () => UpdateConsoleTitle(game);

        game.BlackTimer.OnTimerTick +=
            () => UpdateConsoleTitle(game);

        UpdateConsoleTitle(game);

        while (!gameOver)
        {
            DrawScreen(game);

            var from = AskPieceOption(game);

            if (from is null)
            {
                if (game.GameResult.Type == GameOverType.None)
                {
                    Log.Information(
                        "Game stopped manually by player. " +
                        "CurrentPlayer: {CurrentPlayer}, MoveCount: {MoveCount}",
                        game.CurrentPlayer.PlayerColor,
                        game.GetMoveHistory().Count);
                }

                break;
            }

            var fromPosition = from.Value;

            if (!game.TrySelectPiece(fromPosition))
            {
                Log.Debug(
                    "Player failed to select piece. " +
                    "Player: {Player}, Position: {Position}",
                    game.CurrentPlayer.PlayerColor,
                    ToChessSquare(fromPosition));

                AnsiConsole.MarkupLine(
                    "[red]Bidak tidak dapat dipilih. " +
                    "Silakan pilih ulang.[/]");

                continue;
            }

            var availableMoves =
                game.GetAvailableMovesForSelectedPiece();

            DrawScreen(
                game,
                fromPosition,
                availableMoves);

            var to = AskMoveOption(
                game.GetCurrentBoard(),
                fromPosition,
                availableMoves);

            if (to is null)
            {
                Log.Debug(
                    "Player cancelled destination selection. " +
                    "Player: {Player}, SelectedPosition: {Position}",
                    game.CurrentPlayer.PlayerColor,
                    ToChessSquare(fromPosition));

                continue;
            }

            var toPosition = to.Value;

            var movingPiece = game
                .GetCurrentBoard()
                .Tiles[
                    fromPosition.Row * 8 +
                    fromPosition.Column]
                .ChessPiece;

            var promotionType = PieceType.Queen;

            var isPawnPromotion =
                movingPiece != null &&
                movingPiece.Piece == PieceType.Pawn &&
                (toPosition.Row == 0 ||
                 toPosition.Row == 7) &&
                availableMoves.Exists(move =>
                    move.Row == toPosition.Row &&
                    move.Column == toPosition.Column);

            if (isPawnPromotion)
            {
                promotionType = AskPromotionPiece();
            }

            var moveSucceeded = game.MakeMove(new Move
            {
                From = fromPosition,
                To = toPosition,
                Type = MoveType.Normal,
                PromotionType = promotionType
            });

            if (!moveSucceeded)
            {
                Log.Warning(
                    "Move failed after selection. " +
                    "Player: {Player}, From: {From}, To: {To}",
                    game.CurrentPlayer.PlayerColor,
                    ToChessSquare(fromPosition),
                    ToChessSquare(toPosition));

                AnsiConsole.MarkupLine(
                    "[red]Langkah gagal dijalankan.[/]");
            }
        }

        DrawScreen(game);

        if (gameOver)
        {
            ShowResult(game);

            Log.Information(
                "Game result displayed. Result: {Result}, " +
                "Winner: {Winner}, MoveCount: {MoveCount}",
                game.GameResult.Type,
                game.GameResult.Type == GameOverType.Stalemate
                    ? null
                    : game.GameResult.Winner.ToString(),
                game.GetMoveHistory().Count);
        }
        else
        {
            AnsiConsole.MarkupLine(
                "\n[yellow]Permainan dihentikan oleh pemain.[/]");
        }
    }

    private static void DrawScreen(Game game, Postition? selectedPiece = null, List<Postition>? availableMoves = null)
    {
        AnsiConsole.Clear();
        Color playerColor = game.GetColorsPlayer();
        string displayColorPlayer = playerColor switch
        {
            Color.White => "[bold white] White [/]",
            Color.Black => "[bold grey] Black [/]", 
            _ => throw new ArgumentOutOfRangeException()
        };
    
        AnsiConsole.Write(new Rule("[bold yellow]CHESS CONSOLE[/]").RuleStyle("yellow"));
    
        // 1. Ambil objek visual (IRenderable) dari papan dan tabel
        var boardView = CreateBoardRenderable(game.GetCurrentBoard(), selectedPiece, availableMoves);
        var lastMoveView = CreateLastMoveTable(game.GetMoveHistory());
    
        // 2. Gabungkan secara horizontal menggunakan Columns
        AnsiConsole.Write(new Columns(boardView, lastMoveView));
    
        AnsiConsole.MarkupLine($"\nGiliran: [bold cyan]{Markup.Escape(game.CurrentPlayer.Name)}[/] ({displayColorPlayer})");

        if (availableMoves != null && availableMoves.Count > 0)
        {
            AnsiConsole.MarkupLine($"[green]Terdapat {availableMoves.Count} langkah sah.[/]");
        }
        else if (availableMoves != null && availableMoves.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Bidak ini tidak bisa bergerak kemana-mana![/]");
        }
    }
    private static IRenderable CreateBoardRenderable(Board board, Postition? selectedPiece = null, List<Postition>? availableMoves = null)
    {
        var table = new Table().NoBorder().HideHeaders().Collapse();
        table.AddColumn(new TableColumn(string.Empty).Centered());
        for (var col = 0; col < 8; col++) table.AddColumn(new TableColumn(string.Empty).Centered().NoWrap());

        Pieces? selectedChessPiece = null;
        if (selectedPiece.HasValue)
        {
            var selected = selectedPiece.Value;
            selectedChessPiece = board.Tiles[selected.Row * 8 + selected.Column].ChessPiece;
        }

        for (var row = 7; row >= 0; row--)
        {
            var cells = new List<string> { $"[bold]{row + 1}[/]" };
            for (var col = 0; col < 8; col++)
            {
                var piece = board.Tiles[row * 8 + col].ChessPiece;
                var background = (row + col) % 2 == 0 ? "grey84" : "grey37";

                bool isAvailableMove = availableMoves != null && availableMoves.Exists(m => m.Row == row && m.Column == col);
                bool isCaptureMove = piece != null || (selectedChessPiece?.Piece == PieceType.Pawn && selectedPiece.HasValue && selectedPiece.Value.Column != col);

                if (selectedPiece.HasValue && selectedPiece.Value.Row == row && selectedPiece.Value.Column == col)
                {
                    background = "blue"; 
                }
                else if (isAvailableMove)
                {
                    background = isCaptureMove ? "red" : "green";
                }

                cells.Add($"[on {background}] {GetPieceChar(piece!)} [/]");
            }
            table.AddRow(cells.ToArray());
        }

        table.AddRow("", "[bold]a[/]", "[bold]b[/]", "[bold]c[/]", "[bold]d[/]", "[bold]e[/]", "[bold]f[/]", "[bold]g[/]", "[bold]h[/]");
    
        // Kembalikan objek Align agar dirender oleh Columns di DrawScreen
        return new Align(table, HorizontalAlignment.Center);
    }
    private static IRenderable CreateLastMoveTable(IReadOnlyList<MoveHistory> moveHistory)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.Title = new TableTitle("[bold yellow]⚔ Last Move[/]");

        table.AddColumn(new TableColumn("[bold yellow]#[/]").Centered());
        table.AddColumn(new TableColumn("[bold white]♔ White[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold grey]♚ Black[/]").LeftAligned());

        if (moveHistory.Count == 0)
        {
            table.AddRow("[grey]-[/]", "[grey]Belum ada pergerakan[/]", "[grey]Belum ada pergerakan[/]");
            return table;
        }

        var lastMove = moveHistory[moveHistory.Count - 1];
        var turnNumber = (moveHistory.Count + 1) / 2;
        var whiteText = "[grey]...[/]";
        var blackText = "[grey]...[/]";

        if (lastMove.PieceColor == Color.White)
        {
            whiteText = FormatMove(lastMove);
        }
        else
        {
            blackText = FormatMove(lastMove);
            if (moveHistory.Count >= 2)
            {
                var previousMove = moveHistory[moveHistory.Count - 2];
                if (previousMove.PieceColor == Color.White)
                    whiteText = FormatMove(previousMove);
            }
        }

        table.AddRow($"[bold yellow]{turnNumber}.[/]", whiteText, blackText);
        return table;
    }

    private static void ShowResult(Game game)
    {
        var type = Markup.Escape(game.GameResult.Type.ToString());
        var winner = game.GameResult.Type == GameOverType.Stalemate
            ? "Seri"
            : Markup.Escape(game.GameResult.Winner.ToString());
        AnsiConsole.Write(new Panel($"[bold red]Game Over[/]\nAlasan: [yellow]{type}[/]\nPemenang: [bold green]{winner}[/]")
            .Header("[yellow]Hasil Pertandingan[/]").Border(BoxBorder.Rounded));
    }

    private static string AskPlayerName(string prompt, string fallback)
    {
        var value = AnsiConsole.Ask<string>($"[yellow]{prompt}[/] ([grey]{fallback}[/]):").Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void ShowTitle()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("CHESS CONSOLE").Centered().Color(Spectre.Console.Color.Yellow));
        AnsiConsole.MarkupLine("[grey]Player vs Player • ketik [bold]quit[/] untuk keluar[/]\n");
    }
     private static Postition? AskMoveOption(
        Board board,
        Postition fromPosition,
        List<Postition> availableMoves)
    {
        if (availableMoves == null || availableMoves.Count == 0)
        {
            AnsiConsole.MarkupLine(
                "[red]Bidak yang dipilih tidak mempunyai movement legal.[/]");
            return null;
        }

        const string backOption = "[grey]← Pilih bidak lain[/]";
        var optionPositions = new Dictionary<string, Postition>();
        var choices = new List<string>();
        var selectedPiece = board.Tiles[
            fromPosition.Row * 8 + fromPosition.Column].ChessPiece;

        foreach (var position in availableMoves
                     .OrderBy(move => move.Row)
                     .ThenBy(move => move.Column))
        {
            var square = ToChessSquare(position);
            var targetPiece = board.Tiles[position.Row * 8 + position.Column].ChessPiece;
            var isCapture = targetPiece != null ||
                            (selectedPiece?.Piece == PieceType.Pawn &&
                             fromPosition.Column != position.Column);

            var captureDetail = targetPiece != null
                ? $" [grey](makan {GetPieceChar(targetPiece)})[/]"
                : " [yellow](en passant)[/]";

            var label = isCapture
                ? $"[red]× {square}[/]{captureDetail}"
                : $"[green]→ {square}[/]";

            choices.Add(label);
            optionPositions[label] = position;
        }

        choices.Add(backOption);

        var selectedOption = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Pilih movement yang tersedia:[/]")
                .PageSize(Math.Max(3, Math.Min(10, choices.Count)))
                .HighlightStyle(
                    new Style(
                        Spectre.Console.Color.Black,
                        Spectre.Console.Color.Yellow))
                .MoreChoicesText(
                    "[grey](Gunakan tombol ↑/↓ untuk melihat opsi lain)[/]")
                .AddChoices(choices));

        return selectedOption == backOption
            ? null
            : optionPositions[selectedOption];
    }

    private static Postition? AskSquare(string label, bool allowCancel)
    {
        while (true)
        {
            var hint = allowCancel ? "e4, atau back" : "e2";
            var input = AnsiConsole.Ask<string>($"{label} ([green]{hint}[/]):").Trim();
            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) return null;
            if (allowCancel && input.Equals("back", StringComparison.OrdinalIgnoreCase)) return null;
            if (TryParsePosition(input, out var position)) return position;
            AnsiConsole.MarkupLine("[red]Koordinat tidak valid. Gunakan a1 sampai h8.[/]");
        }
    }
    private static Postition? AskPieceOption(Game game)
    {
        const string quitOption = "[red]✕ Keluar dari permainan[/]";
        const string resignOption = "[darkorange]🏳 Menyerah[/]";
        var board = game.GetCurrentBoard();
        var currentColor = game.CurrentPlayer.PlayerColor;
        var optionPositions = new Dictionary<string, Postition>();
        var choices = new List<string>();

        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                var piece = board.Tiles[row * 8 + column].ChessPiece;
                if (piece == null || piece.PieceColor != currentColor)
                    continue;

                var position = new Postition { Row = row, Column = column };
                var square = ToChessSquare(position);
                var label = $"{GetPieceChar(piece)} [bold]{piece.Piece}[/] [grey]— {square}[/]";

                choices.Add(label);
                optionPositions[label] = position;
            }
        }
        choices.Add(resignOption);
        choices.Add(quitOption);

        var selectedOption = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(
                    $"[yellow]Pilih bidak {Markup.Escape(game.CurrentPlayer.Name)}:[/]")
                .PageSize(Math.Max(3, Math.Min(12, choices.Count)))
                .HighlightStyle(
                    new Style(
                        Spectre.Console.Color.Black,
                        Spectre.Console.Color.Yellow))
                .MoreChoicesText(
                    "[grey](Gunakan tombol ↑/↓ untuk melihat bidak lain)[/]")
                .AddChoices(choices));
        // Evaluasi opsi yang dipilih oleh pemain
        if (selectedOption == quitOption)
        {
            return null; // Menghentikan game tanpa memicu status Game Over
        }
    
        if (selectedOption == resignOption)
        {
            // Panggil metode Resign, yang akan memicu event OnGameOver
            game.Resign(game.CurrentPlayer);
            return null; // Keluar dari loop untuk menampilkan layar hasil
        }

        return selectedOption == quitOption
            ? null
            : optionPositions[selectedOption];
    }


    private static void RenderBoard(Board board, Postition? selectedPiece = null, List<Postition>? availableMoves = null)
    {
        var table = new Table().NoBorder().HideHeaders().Collapse();
        table.AddColumn(new TableColumn(string.Empty).Centered());
        for (var col = 0; col < 8; col++) table.AddColumn(new TableColumn(string.Empty).Centered().NoWrap());

        Pieces? selectedChessPiece = null;
        if (selectedPiece.HasValue)
        {
            var selected = selectedPiece.Value;
            selectedChessPiece = board.Tiles[selected.Row * 8 + selected.Column].ChessPiece;
        }

        for (var row = 7; row >= 0; row--)
        {
            var cells = new List<string> { $"[bold]{row + 1}[/]" };
            for (var col = 0; col < 8; col++)
            {
                var piece = board.Tiles[row * 8 + col].ChessPiece;
                var background = (row + col) % 2 == 0 ? "grey84" : "grey37";

                // Cek apakah koordinat saat ini ada di dalam list availableMoves
                bool isAvailableMove = availableMoves != null && availableMoves.Exists(m => m.Row == row && m.Column == col);
                bool isCaptureMove = piece != null ||
                                     (selectedChessPiece?.Piece == PieceType.Pawn &&
                                      selectedPiece.HasValue &&
                                      selectedPiece.Value.Column != col);

                // Biru: bidak terpilih, hijau: langkah ke tile kosong,
                // merah: langkah yang akan memakan bidak lawan.
                if (selectedPiece.HasValue && selectedPiece.Value.Row == row && selectedPiece.Value.Column == col)
                {
                    background = "blue"; 
                }
                else if (isAvailableMove)
                {
                    background = isCaptureMove ? "red" : "green";
                }

                cells.Add($"[on {background}] {GetPieceChar(piece!)} [/]");
            }
            table.AddRow(cells.ToArray());
        }

        table.AddRow("", "[bold]a[/]", "[bold]b[/]", "[bold]c[/]", "[bold]d[/]",
            "[bold]e[/]", "[bold]f[/]", "[bold]g[/]", "[bold]h[/]");
        AnsiConsole.Write(new Align(table, HorizontalAlignment.Center));
    }
    static string GetPieceChar(Pieces? piece)
    {
        if (piece is null) return " ";
        var symbol = piece.Piece switch
        {
            PieceType.King => piece.PieceColor == Color.White ? "♔" : "♚",
            PieceType.Queen => piece.PieceColor == Color.White ? "♕" : "♛",
            PieceType.Rook => piece.PieceColor == Color.White ? "♖" : "♜",
            PieceType.Bishop => piece.PieceColor == Color.White ? "♗" : "♝",
            PieceType.Knight => piece.PieceColor == Color.White ? "♘" : "♞",
            PieceType.Pawn => piece.PieceColor == Color.White ? "♙" : "♟",
            _ => " "
        };
        return piece.PieceColor == Color.White ? $"[bold white]{symbol}[/]" : $"[bold black]{symbol}[/]";
    }

    
    private static void DrawLastMoveTable(IReadOnlyList<MoveHistory> moveHistory)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.Title = new TableTitle("[bold yellow]⚔ Last Move[/]");

        table.AddColumn(new TableColumn("[bold yellow]#[/]").Centered());
        table.AddColumn(new TableColumn("[bold white]♔ White[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold grey]♚ Black[/]").LeftAligned());

        if (moveHistory.Count == 0)
        {
            table.AddRow(
                "[grey]-[/]",
                "[grey]Belum ada pergerakan[/]",
                "[grey]Belum ada pergerakan[/]");
            AnsiConsole.Write(table);
            return;
        }

        var lastMove = moveHistory[moveHistory.Count - 1];
        var turnNumber = (moveHistory.Count + 1) / 2;
        var whiteText = "[grey]...[/]";
        var blackText = "[grey]...[/]";

        if (lastMove.PieceColor == Color.White)
        {
            whiteText = FormatMove(lastMove);
        }
        else
        {
            blackText = FormatMove(lastMove);
            if (moveHistory.Count >= 2)
            {
                var previousMove = moveHistory[moveHistory.Count - 2];
                if (previousMove.PieceColor == Color.White)
                    whiteText = FormatMove(previousMove);
            }
        }

        table.AddRow($"[bold yellow]{turnNumber}.[/]", whiteText, blackText);
        AnsiConsole.Write(table);
    }
    private static string FormatMove(MoveHistory entry)
    {
        var from = ToChessSquare(entry.Move.From);
        var to = ToChessSquare(entry.Move.To);
        var pieceSymbol = GetPieceChar(new Pieces(entry.PieceColor, entry.PieceType));

        if (entry.Move.Type == MoveType.Castling)
        {
            return $"{pieceSymbol} [dim]({to})[/]";
        }

        if (entry.PieceType == PieceType.Pawn &&
            (entry.Move.To.Row == 0 || entry.Move.To.Row == 7))
        {
            var promotedSymbol = GetPieceChar(
                new Pieces(entry.PieceColor, entry.Move.PromotionType));
            return $"{pieceSymbol} {to} [bold green]={promotedSymbol}[/]";
        }

        if (entry.Move.Type == MoveType.EnPassent)
            return $"{pieceSymbol}[red]×[/] {to}";

        if (entry.CapturedPieceType.HasValue)
        {
            var capturedColor = entry.PieceColor == Color.White
                ? Color.Black
                : Color.White;
            var capturedSymbol = GetPieceChar(
                new Pieces(capturedColor, entry.CapturedPieceType.Value));
            return $"{pieceSymbol} [red]×[/] {to} [red]({capturedSymbol})[/]";
        }

        return $"{pieceSymbol} {to}";
    }
    private static string ToChessSquare(Postition position)
    {
        var column = (char)('A' + position.Column);
        return $"{column}{position.Row + 1}";
    }


    private static bool TryParsePosition(string input, out Postition position)
    {
        position = new Postition { Row = -1, Column = -1 };
        input = input.Trim().ToLowerInvariant();
        if (input.Length != 2) return false;
        var col = input[0] - 'a';
        var row = input[1] - '1';
        if (col is < 0 or > 7 || row is < 0 or > 7) return false;
        position = new Postition { Row = row, Column = col };
        return true;
    }
    private static PieceType AskPromotionPiece()
    {
        var selectedPiece = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Promosikan pion menjadi bidak apa?[/]")
                .PageSize(4)
                .HighlightStyle(new Style(Spectre.Console.Color.Black, Spectre.Console.Color.Yellow))
                .AddChoices("Queen", "Rook", "Bishop", "Knight"));

        return selectedPiece switch
        {
            "Rook" => PieceType.Rook,
            "Bishop" => PieceType.Bishop,
            "Knight" => PieceType.Knight,
            _ => PieceType.Queen
        };
    }
    private static void UpdateConsoleTitle(Game game)
    {
        var whiteTime = FormatTime(game.WhiteTimer.RemainingTime);
        var blackTime = FormatTime(game.BlackTimer.RemainingTime);
        var currentPlayerName = game.CurrentPlayer?.Name ?? "-";

        Console.Title =
            $"Chess Console | Putih {whiteTime} | Hitam {blackTime} | Giliran: {currentPlayerName}";
    }

    private static string FormatTime(TimeSpan time)
    {
        var safeTime = time < TimeSpan.Zero ? TimeSpan.Zero : time;
        return safeTime.TotalHours >= 1
            ? safeTime.ToString(@"hh\:mm\:ss")
            : safeTime.ToString(@"mm\:ss");
    }
    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override(
                "Microsoft",
                LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty(
                "Application",
                "ChessConsole")
            .WriteTo.File(
                path: "logs/chess-.log",
                restrictedToMinimumLevel: LogEventLevel.Debug,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_000_000,
                shared: true,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                "{Level:u3}] " +
                "[GameId: {GameId}] " +
                "[{SourceContext}] " +
                "{Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
