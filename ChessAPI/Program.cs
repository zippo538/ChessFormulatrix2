using System.Text.Json.Serialization;
using AutoMapper;
using ChessAPI.Profiles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)
    );
});;
builder.Services.AddAutoMapper(config => 
{
    config.AddProfile<ChessProfile>();
});

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

