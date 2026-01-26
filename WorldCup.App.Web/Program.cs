using WorldCup.App.Shared.Services;
using WorldCup.App.Web.Components;

var builder = WebApplication.CreateBuilder(args);



// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient apuntando a la API (PUERTO 7092)
builder.Services.AddHttpClient<PartidosService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7092/");
});

builder.Services.AddHttpClient<PrediccionesService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7092/");
});

builder.Services.AddHttpClient<PollasService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7092/");
});

builder.Services.AddScoped<SesionService>();





// en la linea anterior se van agregando los servicios
//builder.Services.AddScoped<PrediccionesService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(WorldCup.App.Shared._Imports).Assembly);

app.Run();
