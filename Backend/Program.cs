using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Permissive CORS Rules (Essential for Frontend Communication)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. Configure MySQL Database Connection via Pomelo Driver
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(dbConnectionString, ServerVersion.AutoDetect(dbConnectionString)));

// 3. Configure Redis Connection with Resilient Startup Options
var redisConnectionString = builder.Configuration.GetValue<string>("RedisConnection") ?? "localhost:6379";

// Parse the connection string into options and disable strict instant connection aborts
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false; 

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

var app = builder.Build();

// CRITICAL: Apply CORS middleware before mapping routing endpoints
app.UseCors();

// 4. API Core Processing Route (Cache-Aside Pattern)
app.MapGet("/api/data", async (IConnectionMultiplexer redis, AppDbContext db) =>
{
    try
    {
        var cache = redis.GetDatabase();
        string? cachedMessage = await cache.StringGetAsync("dashboard_message");

        // Cache Hit Rule
        if (!string.IsNullOrEmpty(cachedMessage))
        {
            return Results.Ok(new { message = cachedMessage, cached = true });
        }

        // Cache Miss Rule -> Query MySQL Database
        var dbItem = await db.Settings.FirstOrDefaultAsync(s => s.Key == "WelcomeMessage");
        string message = dbItem?.Value ?? "Hello from the MySQL Database Database!";

        // Push to Redis Cache with a 30-second lifecycle
        await cache.StringSetAsync("dashboard_message", message, TimeSpan.FromSeconds(30));

        return Results.Ok(new { message, cached = false });
    }
    catch (Exception ex)
    {
        return Results.Json(new { message = $"Backend Error: {ex.Message}", cached = false }, statusCode: 500);
    }
});

// 5. Consolidated Database Preparation Pipeline
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        
        // Use EnsureCreated to reliably generate schema without managing manual migration configurations
        db.Database.EnsureCreated();
        
        // Data Seed Check
        if (!db.Settings.Any())
        {
            db.Settings.Add(new Setting 
            { 
                Key = "WelcomeMessage", 
                Value = "Connected! Fetching live from your MySQL database container." 
            });
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding or establishing the database.");
    }
}

app.Run("http://0.0.0.0:5000");

// --- DATA STRUCTURE ENTITIES ---
public class AppDbContext : DbContext 
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Setting> Settings => Set<Setting>();
}

public class Setting 
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}