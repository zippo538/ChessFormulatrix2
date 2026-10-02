using System.Text.Json.Serialization;
using Serilog;
using Serilog.Events;
using ChessAPI.Profiles;


ConfigureLogging();
try
{
    Log.Information("Chess API application started");
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false)
        );
    });
    
    builder.Services.AddAutoMapper(config => { config.AddProfile<ChessProfile>(); });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", policy =>
        {
            // Sesuaikan URL ini dengan port React Anda (biasanya 3000 untuk CRA atau 5173 untuk Vite)
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    var app = builder.Build();
    app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowReactApp");

    app.UseAuthorization();

// 7. Memetakan semua endpoint yang ada di dalam ChessController
    app.MapControllers();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Unhandled exception");
    Console.WriteLine("Terjadi kesalahan pada API");
}
finally
{
    Log.Information("Chess API stopped");
    Log.CloseAndFlush();
}

void ConfigureLogging()
{
    const string outputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] " +
        "[GameId: {GameId}] " +
        "[{SourceContext}] " +
        "{Message:lj}{NewLine}{Exception}";

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override(
            "Microsoft",
            LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty(
            "Application",
            "ChessFormulatrix.WebAPI")

        .WriteTo.Console(
            restrictedToMinimumLevel: LogEventLevel.Debug,
            outputTemplate: outputTemplate)

        .WriteTo.File(
            path: "logs/chess-API.log",
            restrictedToMinimumLevel: LogEventLevel.Debug,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: 10_000_000,
            shared: true,
            outputTemplate: outputTemplate)
        .CreateLogger();
}

public partial class Program
{
}