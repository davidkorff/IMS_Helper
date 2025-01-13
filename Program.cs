using Microsoft.AspNetCore.Builder;
using ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using ApiGateway.Authorization;
using Microsoft.AspNetCore.Identity;
using ApiGateway.Services;
using ApiGateway.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add API Key Authentication
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", opts => { });

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireApiKey", policy =>
        policy.RequireAuthenticationSchemes("ApiKey"));
        
    options.AddPolicy("RequireProductionKey", policy =>
        policy.RequireAuthenticationSchemes("ApiKey")
             .AddRequirements(new ApiKeyEnvironmentRequirement("Production")));
             
    options.AddPolicy("RequireQuotePermission", policy =>
        policy.RequireAuthenticationSchemes("ApiKey")
             .AddRequirements(new ApiKeyPermissionRequirement("quotes:create")));
});

// Register handlers
builder.Services.AddScoped<IAuthorizationHandler, ApiKeyAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ApiKeyEnvironmentHandler>();

// Add authentication and authorization services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = 
        builder.Configuration.GetValue<bool>("Security:RequireConfirmedEmail");
    
    options.Password.RequireDigit = 
        builder.Configuration.GetValue<bool>("Security:PasswordRequiresDigit");
    options.Password.RequireLowercase = 
        builder.Configuration.GetValue<bool>("Security:PasswordRequiresLowercase");
    options.Password.RequireUppercase = 
        builder.Configuration.GetValue<bool>("Security:PasswordRequiresUppercase");
    options.Password.RequireNonAlphanumeric = 
        builder.Configuration.GetValue<bool>("Security:PasswordRequiresNonAlphanumeric");
    options.Password.RequiredLength = 
        builder.Configuration.GetValue<int>("Security:PasswordMinLength");

    options.Lockout.MaxFailedAccessAttempts = 
        builder.Configuration.GetValue<int>("Security:LockoutMaxFailedAttempts");
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("Security:LockoutDurationMinutes"));
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Register services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseValidationMiddleware();

app.MapControllers();

app.Run(); 