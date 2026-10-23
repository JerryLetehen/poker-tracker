using Microsoft.EntityFrameworkCore;
using poker_web_api.Auth;
using poker_web_api.Configuration;
using poker_web_api.Data;
using poker_web_api.Endpoints;
using poker_web_api.Endpoints.Public;
using poker_web_api.Endpoints.Admin;
using poker_web_api.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration binding (appsettings = local defaults, env vars = secrets) ---
var authOptions = new TelegramAuthOptions();
builder.Configuration.GetSection(TelegramAuthOptions.SectionName).Bind(authOptions);
// Allow a comma-separated env var form for bootstrap admin ids (easier than indexed env vars).
ConfigurationHelpers.ApplyBootstrapAdminIdsOverride(builder.Configuration, authOptions);

var corsOptions = new CorsOptions();
builder.Configuration.GetSection(CorsOptions.SectionName).Bind(corsOptions);

var frontendOptions = new FrontendHostingOptions();
builder.Configuration.GetSection(FrontendHostingOptions.SectionName).Bind(frontendOptions);

// Fail fast if a deployed environment is missing required secrets.
var isProductionLike = !builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing");
ConfigurationValidator.ValidateOrThrow(authOptions, corsOptions, isProductionLike);

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(corsOptions);
builder.Services.AddSingleton(frontendOptions);

// --- Auth / access-control services ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelegramInitDataValidator>(
    _ => new TelegramInitDataValidator(authOptions.InitDataMaxAgeSeconds));
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddScoped<PendingAccessService>();
builder.Services.AddHttpClient<ITelegramBotClient, TelegramBotClient>()
    // The request URL contains the bot token; suppress HttpClient logging for this
    // client so the token is never written to logs.
    .RemoveAllLoggers();
builder.Services.AddScoped<BotWebhookService>();

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOptions.AllowedOrigins.Count > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            // No origins configured (e.g. local dev behind the Vite proxy, where
            // requests are same-origin and CORS is not exercised).
            policy.AllowAnyHeader().AllowAnyMethod();
        }
    });
});
builder.Services.AddDbContext<PokerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));

var app = builder.Build();

// Apply pending migrations and seed the bootstrap admin(s) on startup.
// (Skipped for the in-memory provider used by the test host.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    await BootstrapAdminSeeder.SeedAsync(db, authOptions);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Strip the "/api" prefix (single-app deployment) BEFORE routing runs, so endpoint
// matching sees the real path (e.g. "/player-list") rather than "/api/player-list".
// This must come before UseRouting, otherwise routing selects the SPA fallback for
// "/api/..." and the rewrite happens too late.
app.UseMiddleware<ApiPathPrefixMiddleware>();

app.UseRouting();

app.UseCors();

// Serve the bundled SPAs (prod single-app image). Static assets are public and must not
// be gated by the Telegram auth middleware, so this runs before it. No-op when disabled.
app.UseBundledFrontends(frontendOptions);

// Authenticate + authorize every API request (fail-closed) before it reaches an endpoint.
app.UseMiddleware<TelegramAuthMiddleware>();

// Unauthenticated liveness probe (used by Docker/compose health checks).
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

// Public endpoints
app.MapGetPlayerList();
app.MapGetPlayerProgression();
app.MapGetLeaderboard();
app.MapGetLeaderboardHistory();
app.MapGetGames();
app.MapGetGameDetail();
app.MapGetRecords();

// Telegram bot webhook (secured by its own secret path segment, not Mini App auth)
app.MapBotWebhook();

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
app.MapGetDbStatus();
app.MapCheckpointDb();
app.MapBackupDb();
app.MapAccessManagement();

app.Run();

// Make Program accessible to test project
public partial class Program { }
