using Microsoft.EntityFrameworkCore;
using poker_web_api.Data;
using poker_web_api.Endpoints.Public;
using poker_web_api.Endpoints.Admin;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<PokerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

// Public endpoints
app.MapGetPlayerList();
app.MapGetPlayerProgression();
app.MapGetLeaderboard();
app.MapGetLeaderboardHistory();
app.MapGetGames();
app.MapGetGameDetail();
app.MapGetRecords();

// Admin endpoints
app.MapGetActiveGame();
app.MapCreateGame();
app.MapRecordBuyIn();
app.MapRecordCashOut();
app.MapDeleteEntry();
app.MapDeleteGame();
app.MapCreatePlayer();
app.MapGetPlayersWithIds();
app.MapRenamePlayer();
app.MapGetAdminGameDetail();

app.Run();