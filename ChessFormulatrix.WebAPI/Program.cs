using System.Text.Json.Serialization;
using ChessFormulatrix.WebAPI;
using ChessFormulatrix.WebAPI.Hubs;
using ChessFormulatrix.WebAPI.Services;
using Serilog;
using Serilog.Events;

const string OutputTemplate =
    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] " +
    "[{SourceContext}] " +
    "{Message:lj}{NewLine}{Exception}";

// Bootstrap logger: harus di luar try supaya error saat startup pun tercatat.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ChessFormulatrix.WebAPI")
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate: OutputTemplate)
    .WriteTo.File(
        path: "logs/chess-API.log",
        restrictedToMinimumLevel: LogEventLevel.Debug,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 10_000_000,
        shared: true,
        outputTemplate: OutputTemplate)
    .CreateLogger();

try
{
    Log.Information("Chess API application started");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: false)));

    builder.Services.AddAutoMapper(config => config.AddProfile<ChessProfile>());

    // Shared services
    builder.Services.AddSingleton<GameSessionService>();
    builder.Services.AddSingleton<LobbyService>();

    // SignalR
    builder.Services.AddSignalR();

    builder.Services.AddCors(options =>
        options.AddPolicy("AllowReactApp", policy => policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:8080")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

    WebApplication app = builder.Build();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowReactApp");
    app.UseAuthorization();
    app.MapControllers();

    // SignalR hub endpoints
    app.MapHub<LobbyHub>("/hubs/lobby");
    app.MapHub<GameHub>("/hubs/game");

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Chess API terminated unexpectedly");
}
finally
{
    Log.Information("Chess API stopped");
    await Log.CloseAndFlushAsync();
}

// Dibutuhkan WebApplicationFactory<Program> pada integration test.
public partial class Program;
