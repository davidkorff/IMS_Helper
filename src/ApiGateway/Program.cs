using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
builder.Services.AddDbContext<UsageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Add services
builder.Services.AddScoped<IUsageTracker, UsageTracker>();
builder.Services.AddScoped<IIMSClient, IMSClient>();

// Add monitoring
builder.Services.AddMonitoring(builder.Configuration);

// Add services
builder.Services.AddErrorHandling();

// Add services
builder.Services.AddApiLogging(builder.Configuration);

// Add services
builder.Services.AddApiRouting(builder.Configuration);

// Add services
builder.Services.AddApiRateLimiting(builder.Configuration);

// Add Swagger documentation
builder.Services.AddSwaggerDocumentation(builder.Configuration);

// Add transformation services
builder.Services.AddTransformation(builder.Configuration);

var app = builder.Build();

// Use middleware (before routing)
app.UseErrorHandling();

// Add middleware in the correct order
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Add the error handling middleware before other middleware that might throw exceptions
app.UseIMSErrorHandling(includeDetails: app.Environment.IsDevelopment());

// Configure middleware
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<UsageTrackingMiddleware>();

// Configure Swagger documentation
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwaggerDocumentation(builder.Configuration);
}

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsageDbContext>();
    db.Database.Migrate();
}

// Add monitoring endpoints
app.UseMonitoring();

// Use middleware (after UseErrorHandling)
app.UseApiLogging();

// Use middleware (after authentication/authorization)
app.UseApiRouting();

// Use middleware (after authentication, before routing)
app.UseApiRateLimiting();

// Use transformation middleware (after routing, before endpoints)
app.UseRouting();
app.UseTransformation();

app.MapControllers();

app.Run(); 