using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Gamify.Api;
using Gamify.Api.Ai;
using Gamify.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .AllowAnyHeader()));

var configuredKey = builder.Configuration["Jwt:SigningKey"];
var keyBytes = string.IsNullOrWhiteSpace(configuredKey)
    ? RandomNumberGenerator.GetBytes(32)
    : Encoding.UTF8.GetBytes(configuredKey);
if (keyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");

var signingKey = new SymmetricSecurityKey(keyBytes);
builder.Services.AddSingleton(new JwtSessions(signingKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = JwtSessions.Issuer,
        ValidAudience = JwtSessions.Audience,
        IssuerSigningKey = signingKey,
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!JwtSessions.TryReadClaims(context.Principal, out var userId, out var sessionId))
            {
                context.Fail("Invalid session claims.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<GamifyDbContext>();
            var active = await db.UserSessions.AnyAsync(session => session.Id == sessionId && session.UserId == userId,
                context.HttpContext.RequestAborted);
            if (!active)
                context.Fail("Session ended.");
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("recommendations", context => RateLimitPartition.GetFixedWindowLimiter(
        JwtSessions.TryReadClaims(context.User, out var userId, out _) ? userId.ToString() : "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("store-clicks", context => RateLimitPartition.GetFixedWindowLimiter(
        JwtSessions.TryReadClaims(context.User, out var userId, out _) ? userId.ToString() : "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

var connectionString = builder.Configuration.GetConnectionString("Gamify")
    ?? throw new InvalidOperationException("The Gamify SQLite connection string is missing.");
var sqlite = new SqliteConnectionStringBuilder(connectionString);
if (sqlite.DataSource != ":memory:")
{
    sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, builder.Environment.ContentRootPath);
}
builder.Services.AddDbContext<GamifyDbContext>(options => options.UseSqlite(sqlite.ToString()));

builder.Services.AddSingleton<IOptions<AiOptions>>(
    Options.Create(AiOptions.Load(builder.Environment.ContentRootPath)));
builder.Services.AddHttpClient<OpenAiResponsesClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<AiOptions>>().Value;
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
}).RedactLoggedHeaders(["Authorization"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
