using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using WorldCup.Api.Data;
using WorldCup.Api.Services;

var builder = WebApplication.CreateBuilder(args);
const string ApiKeyHeaderName = "X-WorldCup-Api-Key";

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection antes de iniciar la API.");
}

var requireApiKey = builder.Configuration.GetValue<bool?>("ApiAccess:RequireKey") ??
    !builder.Environment.IsDevelopment();
var apiAccessKey = builder.Configuration["ApiAccess:Key"];

// DbContext (Postgres)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AdminAuthorizationService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<FormatoManualPdfService>();
builder.Services.AddSingleton<AttemptRateLimiter>();

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(jwtKey) ||
     jwtKey.Length < 32 ||
     jwtKey == "PonUnaClaveMuyLargaAqui123!$" ||
     jwtKey == "SuperClaveTemporal123!"))
{
    throw new InvalidOperationException("Configure Jwt:Key con una clave segura antes de ejecutar en produccion.");
}

if (requireApiKey && string.IsNullOrWhiteSpace(apiAccessKey))
{
    throw new InvalidOperationException("Configure ApiAccess:Key para proteger la API antes de publicar.");
}

ValidarConfiguracionProduccion(builder.Configuration, builder.Environment);

var key = Encoding.UTF8.GetBytes(jwtKey ?? "SuperClaveTemporal123!");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Controllers + Swagger (forma compatible con .NET 8)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await DatabaseBootstrapper.AplicarAjustesCompatibilidadAsync(app.Services);

// middleware
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        return Task.CompletedTask;
    });

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Swagger middleware (uso estándar en .NET 8)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (requireApiKey)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next();
            return;
        }

        var providedKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        if (!ApiKeysMatch(apiAccessKey, providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API key invalida o ausente.");
            return;
        }

        await next();
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    app = "WorldCup.Api",
    utc = DateTime.UtcNow
}));

app.MapControllers();

app.Run();

static bool ApiKeysMatch(string? expected, string? provided)
{
    if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
    {
        return false;
    }

    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var providedBytes = Encoding.UTF8.GetBytes(provided);

    return expectedBytes.Length == providedBytes.Length &&
        CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
}

static void ValidarConfiguracionProduccion(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (environment.IsDevelopment())
    {
        return;
    }

    var adminEmails = configuration.GetSection("AdminSettings:Emails").Get<string[]>() ?? Array.Empty<string>();
    var adminIds = configuration.GetSection("AdminSettings:UserIds").Get<int[]>() ?? Array.Empty<int>();
    if (!adminEmails.Any(email => !string.IsNullOrWhiteSpace(email)) && adminIds.Length == 0)
    {
        throw new InvalidOperationException("Configure al menos un administrador en AdminSettings:Emails o AdminSettings:UserIds.");
    }

    var smtp = configuration.GetSection("SmtpSettings");
    var smtpKeys = new[] { "Host", "User", "Password", "FromEmail" };
    if (smtpKeys.Any(key => string.IsNullOrWhiteSpace(smtp[key])))
    {
        throw new InvalidOperationException("Configure SmtpSettings:Host, User, Password y FromEmail antes de publicar.");
    }
}
