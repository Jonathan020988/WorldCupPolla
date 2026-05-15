using WorldCup.App.Shared.Services;
using Microsoft.AspNetCore.HttpOverrides;
using WorldCup.App.Web.Components;

var builder = WebApplication.CreateBuilder(args);
const string ApiKeyHeaderName = "X-WorldCup-Api-Key";

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7092/";
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("Configure ApiBaseUrl con una URL absoluta valida.");
}

var apiAccessKey = builder.Configuration["ApiAccess:Key"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiAccessKey))
{
    throw new InvalidOperationException("Configure ApiAccess:Key con la misma llave privada configurada en la API.");
}

void ConfigureApiClient(HttpClient client)
{
    client.BaseAddress = apiBaseUri;

    if (!string.IsNullOrWhiteSpace(apiAccessKey))
    {
        client.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        client.DefaultRequestHeaders.Add(ApiKeyHeaderName, apiAccessKey);
    }
}

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<LocalStorageService>();

builder.Services.AddScoped<SesionService>();

builder.Services.AddScoped<UsuariosService>();

builder.Services.AddScoped(sp =>
{
    var client = new HttpClient();
    ConfigureApiClient(client);
    return client;
});


// HttpClient apuntando a la API (PUERTO 7092)
builder.Services.AddHttpClient<AuthService>(ConfigureApiClient);

builder.Services.AddHttpClient<PartidosService>(ConfigureApiClient);

builder.Services.AddHttpClient<PrediccionesService>(ConfigureApiClient);

builder.Services.AddHttpClient<PollasService>(ConfigureApiClient);

// en la linea anterior se van agregando los servicios
//builder.Services.AddScoped<PrediccionesService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    app = "WorldCup.App.Web",
    utc = DateTime.UtcNow
}));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(WorldCup.App.Shared._Imports).Assembly);

app.Run();
