using AutoMapper;
using ChessAPI.Controllers;
using ChessConsole.Model;
using ChessWeb.DTO;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace Game.Tests.Controllers;

public class ChessControllerTest
{
     private Mock<IMapper> _mapperMock = null!;
    private ChessController _controller = null!;

    private BoardDTO _boardDto = null!;
    private List<PosititionDTO> _validMovesDto = null!;
    private List<MoveHistoryDTO> _historyDto = null!;
    private MoveDTO _suggestionDto = null!;

    [SetUp]
    public void SetUp()
    {
        _mapperMock = new Mock<IMapper>();

        _boardDto = new BoardDTO();

        _validMovesDto = new List<PosititionDTO>
        {
            new() { Row = 2, Column = 0 },
            new() { Row = 3, Column = 0 }
        };

        _historyDto = new List<MoveHistoryDTO>();

        _suggestionDto = new MoveDTO
        {
            From = new PosititionDTO
            {
                Row = 0,
                Column = 1
            },
            To = new PosititionDTO
            {
                Row = 2,
                Column = 2
            }
        };

        _mapperMock
            .Setup(mapper => mapper.Map<BoardDTO>(It.IsAny<object>()))
            .Returns(_boardDto);

        _mapperMock
            .Setup(mapper =>
                mapper.Map<List<PosititionDTO>>(It.IsAny<object>()))
            .Returns(_validMovesDto);

        _mapperMock
            .Setup(mapper => mapper.Map<MoveDTO>(It.IsAny<object>()))
            .Returns(_suggestionDto);

        _mapperMock
            .Setup(mapper =>
                mapper.Map<List<MoveHistoryDTO>>(It.IsAny<object>()))
            .Returns(_historyDto);

        _controller = new ChessController(_mapperMock.Object);
    }

    // =========================================================
    // CREATE GAME
    // =========================================================

    [Test]
    public void CreateGame_WithValidRequest_ReturnsOkAndGameId()
    {
        var request = new CreateGameDTO
        {
            WhiteName = "Player One",
            BlackName = "Player Two",
            VsBot = false,
            Minutes = 10,
            ClockEnabled = false
        };

        var result = _controller.CreateGame(request);

        var okResult = result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));
        Assert.That(ReadProperty<string>(okResult.Value!, "GameId"),
            Is.Not.Null.And.Not.Empty);
        Assert.That(ReadProperty<string>(okResult.Value!, "Message"),
            Is.EqualTo("Game started"));
        Assert.That(ReadProperty<bool>(okResult.Value!, "VsBot"),
            Is.False);
        Assert.That(ReadProperty<int>(okResult.Value!, "Minutes"),
            Is.EqualTo(10));
        Assert.That(ReadProperty<bool>(okResult.Value!, "ClockEnabled"),
            Is.False);
    }

    [TestCase(0, 1)]
    [TestCase(-10, 1)]
    [TestCase(181, 180)]
    [TestCase(500, 180)]
    public void CreateGame_WhenMinutesOutsideRange_ClampsMinutes(
        int requestedMinutes,
        int expectedMinutes)
    {
        var request = new CreateGameDTO
        {
            WhiteName = "White",
            BlackName = "Black",
            VsBot = false,
            Minutes = requestedMinutes,
            ClockEnabled = false
        };

        var result = _controller.CreateGame(request);

        var okResult = result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(
            ReadProperty<int>(okResult!.Value!, "Minutes"),
            Is.EqualTo(expectedMinutes));
    }

    // =========================================================
    // GET BOARD
    // =========================================================

    [Test]
    public void GetBoard_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.GetBoard("unknown-game");

        var notFoundResult = result.Result as NotFoundObjectResult;

        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(
            notFoundResult!.Value,
            Is.EqualTo("Game session not found."));
    }

    [Test]
    public void GetBoard_WhenGameExists_ReturnsMappedBoard()
    {
        var gameId = CreateGame();

        var result = _controller.GetBoard(gameId);

        var okResult = result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.Value, Is.SameAs(_boardDto));

        _mapperMock.Verify(
            mapper => mapper.Map<BoardDTO>(It.IsAny<object>()),
            Times.Once);
    }

    // =========================================================
    // VALID MOVES
    // =========================================================

    [Test]
    public void GetValidMoves_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.GetValidMoves(
            "unknown-game",
            row: 1,
            col: 0);

        var notFoundResult = result.Result as NotFoundObjectResult;

        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(
            notFoundResult!.Value,
            Is.EqualTo("Game session not found."));
    }

    [Test]
    public void GetValidMoves_WhenWhitePawnSelected_ReturnsOk()
    {
        var gameId = CreateGame();

        // Pion putih awal berada pada row 1.
        var result = _controller.GetValidMoves(
            gameId,
            row: 1,
            col: 0);

        var okResult = result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.Value, Is.SameAs(_validMovesDto));

        _mapperMock.Verify(
            mapper => mapper.Map<List<PosititionDTO>>(
                It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void GetValidMoves_WhenEmptyTileSelected_ReturnsBadRequest()
    {
        var gameId = CreateGame();

        var result = _controller.GetValidMoves(
            gameId,
            row: 4,
            col: 4);

        var badRequest = result.Result as BadRequestObjectResult;

        Assert.That(badRequest, Is.Not.Null);
        Assert.That(
            badRequest!.Value,
            Is.EqualTo(
                "Tidak ada bidak milik pemain saat ini di koordinat tersebut."));
    }

    [Test]
    public void GetValidMoves_WhenOpponentPieceSelected_ReturnsBadRequest()
    {
        var gameId = CreateGame();

        // Saat permainan dimulai, giliran putih.
        // Row 6 berisi pion hitam.
        var result = _controller.GetValidMoves(
            gameId,
            row: 6,
            col: 0);

        Assert.That(
            result.Result,
            Is.TypeOf<BadRequestObjectResult>());
    }

    // =========================================================
    // MAKE MOVE
    // =========================================================

    [Test]
    public void MakeMove_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var moveDto = CreateMove(
            fromRow: 1,
            fromColumn: 4,
            toRow: 3,
            toColumn: 4);

        var result = _controller.MakeMove(
            "unknown-game",
            moveDto);

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void MakeMove_WhenSelectionIsInvalid_ReturnsBadRequest()
    {
        var gameId = CreateGame();

        // Row 4 column 4 kosong pada posisi awal.
        var moveDto = CreateMove(
            fromRow: 4,
            fromColumn: 4,
            toRow: 5,
            toColumn: 4);

        var result = _controller.MakeMove(gameId, moveDto);

        var badRequest = result as BadRequestObjectResult;

        Assert.That(badRequest, Is.Not.Null);
        Assert.That(
            badRequest!.Value,
            Is.EqualTo("Invalid selection."));
    }

    [Test]
    public void MakeMove_WhenDestinationIsIllegal_ReturnsBadRequest()
    {
        var gameId = CreateGame();

        // Pion putih tidak dapat bergerak tiga petak.
        var moveDto = CreateMove(
            fromRow: 1,
            fromColumn: 4,
            toRow: 4,
            toColumn: 4);

        var result = _controller.MakeMove(gameId, moveDto);

        var badRequest = result as BadRequestObjectResult;

        Assert.That(badRequest, Is.Not.Null);
        Assert.That(
            badRequest!.Value,
            Is.EqualTo("Illegal move."));
    }

    [Test]
    public void MakeMove_WhenMoveIsLegal_ReturnsOkAndChangesTurn()
    {
        var gameId = CreateGame(vsBot: false);

        // Pion putih e2 ke e4 berdasarkan koordinat engine.
        var moveDto = CreateMove(
            fromRow: 1,
            fromColumn: 4,
            toRow: 3,
            toColumn: 4);

        var result = _controller.MakeMove(gameId, moveDto);

        var okResult = result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                ReadProperty<bool>(
                    okResult!.Value!,
                    "GameOver"),
                Is.False);

            Assert.That(
                ReadProperty<string>(
                    okResult.Value!,
                    "CurrentTurn"),
                Is.EqualTo("Black"));

            Assert.That(
                ReadNullableProperty(
                    okResult.Value!,
                    "BotMove"),
                Is.Null);
        });
    }

    [Test]
    public void MakeMove_WhenPromotionTypeIsProvided_ProcessesRequest()
    {
        var gameId = CreateGame(vsBot: false);

        // Pengujian ini memastikan parsing PromotionType tidak
        // mengganggu langkah normal.
        var moveDto = CreateMove(
            fromRow: 1,
            fromColumn: 0,
            toRow: 2,
            toColumn: 0,
            promotionType: "Queen");

        var result = _controller.MakeMove(gameId, moveDto);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    // =========================================================
    // SUGGESTION
    // =========================================================

    [Test]
    public void GetSuggestion_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.GetSuggestion(
            "unknown-game",
            row: 0,
            col: 1);

        Assert.That(
            result.Result,
            Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void GetSuggestion_WhenPieceHasLegalMove_ReturnsSuggestion()
    {
        var gameId = CreateGame();

        // Knight putih pada posisi awal.
        var result = _controller.GetSuggestion(
            gameId,
            row: 0,
            col: 1);

        var okResult = result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.Value, Is.SameAs(_suggestionDto));

        _mapperMock.Verify(
            mapper => mapper.Map<MoveDTO>(It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void GetSuggestion_WhenEmptyTileSelected_ReturnsNotFound()
    {
        var gameId = CreateGame();

        var result = _controller.GetSuggestion(
            gameId,
            row: 4,
            col: 4);

        var notFoundResult = result.Result as NotFoundObjectResult;

        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(
            notFoundResult!.Value,
            Is.EqualTo("Tidak ada saran langkah."));
    }

    // =========================================================
    // BOT MOVE
    // =========================================================

    [Test]
    public void MakeBotMove_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.MakeBotMove("unknown-game");

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void MakeBotMove_WhenGameIsPlayerVsPlayer_ReturnsBadRequest()
    {
        var gameId = CreateGame(vsBot: false);

        var result = _controller.MakeBotMove(gameId);

        var badRequest = result as BadRequestObjectResult;

        Assert.That(badRequest, Is.Not.Null);
        Assert.That(
            badRequest!.Value,
            Is.EqualTo(
                "Game ini adalah mode player vs player, tidak ada Bot untuk dijalankan."));
    }

    // =========================================================
    // TIME STATUS
    // =========================================================

    [Test]
    public void GetTimeStatus_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.GetTimeStatus("unknown-game");

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void GetTimeStatus_WhenGameExists_ReturnsCurrentStatus()
    {
        var gameId = CreateGame(
            vsBot: false,
            minutes: 10,
            clockEnabled: false);

        var result = _controller.GetTimeStatus(gameId);

        var okResult = result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                ReadProperty<string>(
                    okResult!.Value!,
                    "CurrentTurn"),
                Is.EqualTo("White"));

            Assert.That(
                ReadProperty<bool>(
                    okResult.Value!,
                    "TimeExpired"),
                Is.False);

            Assert.That(
                ReadProperty<bool>(
                    okResult.Value!,
                    "GameOver"),
                Is.False);

            Assert.That(
                ReadProperty<string>(
                    okResult.Value!,
                    "Reason"),
                Is.EqualTo("None"));

            Assert.That(
                ReadNullableProperty(
                    okResult.Value!,
                    "Winner"),
                Is.Null);
        });
    }

    // =========================================================
    // RESIGN
    // =========================================================

    [Test]
    public void Resign_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.Resign("unknown-game");

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void Resign_WhenWhitePlayerResigns_ReturnsBlackAsWinner()
    {
        var gameId = CreateGame();

        var result = _controller.Resign(gameId);

        var okResult = result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                ReadProperty<bool>(
                    okResult!.Value!,
                    "GameOver"),
                Is.True);

            Assert.That(
                ReadProperty<string>(
                    okResult.Value!,
                    "Reason"),
                Is.EqualTo("Resigned"));

            Assert.That(
                ReadProperty<string>(
                    okResult.Value!,
                    "Winner"),
                Is.EqualTo("Black"));
        });
    }

    // =========================================================
    // MOVE HISTORY
    // =========================================================

    [Test]
    public void GetMoveHistory_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = _controller.GetMoveHistory("unknown-game");

        Assert.That(
            result.Result,
            Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void GetMoveHistory_WhenGameExists_ReturnsMappedHistory()
    {
        var gameId = CreateGame();

        var result = _controller.GetMoveHistory(gameId);

        var okResult = result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.Value, Is.SameAs(_historyDto));

        _mapperMock.Verify(
            mapper => mapper.Map<List<MoveHistoryDTO>>(
                It.IsAny<object>()),
            Times.Once);
    }

    // =========================================================
    // TEST HELPERS
    // =========================================================

    private string CreateGame(
        bool vsBot = false,
        int minutes = 10,
        bool clockEnabled = false)
    {
        var request = new CreateGameDTO
        {
            WhiteName = "White Player",
            BlackName = "Black Player",
            VsBot = vsBot,
            Minutes = minutes,
            ClockEnabled = clockEnabled
        };

        var result = _controller.CreateGame(request);

        var okResult = result as OkObjectResult;

        Assert.That(
            okResult,
            Is.Not.Null,
            "CreateGame harus mengembalikan HTTP 200.");

        return ReadProperty<string>(
            okResult!.Value!,
            "GameId");
    }

    private static MoveDTO CreateMove(
        int fromRow,
        int fromColumn,
        int toRow,
        int toColumn,
        string? promotionType = null)
    {
        return new MoveDTO
        {
            From = new PosititionDTO
            {
                Row = fromRow,
                Column = fromColumn
            },
            To = new PosititionDTO
            {
                Row = toRow,
                Column = toColumn
            },
            PromotionType = promotionType
        };
    }

    private static T ReadProperty<T>(
        object source,
        string propertyName)
    {
        var property = source
            .GetType()
            .GetProperty(propertyName);

        Assert.That(
            property,
            Is.Not.Null,
            $"Properti '{propertyName}' tidak ditemukan.");

        var value = property!.GetValue(source);

        Assert.That(
            value,
            Is.Not.Null,
            $"Nilai properti '{propertyName}' adalah null.");

        return (T)value!;
    }

    private static object? ReadNullableProperty(
        object source,
        string propertyName)
    {
        var property = source
            .GetType()
            .GetProperty(propertyName);

        Assert.That(
            property,
            Is.Not.Null,
            $"Properti '{propertyName}' tidak ditemukan.");

        return property!.GetValue(source);
    }
}