namespace WorldCup.Api.Services;

public class LiveScoreSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LiveScoreSyncHostedService> _logger;

    public LiveScoreSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<LiveScoreSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var habilitado = _configuration.GetValue<bool?>("MarcadoresEnVivo:Enabled") ?? false;
        if (!habilitado)
        {
            _logger.LogInformation("Sincronizacion de marcadores en vivo deshabilitada por configuracion.");
            return;
        }

        var intervaloSegundos = Math.Clamp(
            _configuration.GetValue<int?>("MarcadoresEnVivo:IntervaloSegundos") ?? 30,
            15,
            300);

        await SincronizarUnaVezAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervaloSegundos));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SincronizarUnaVezAsync(stoppingToken);
        }
    }

    private async Task SincronizarUnaVezAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<LiveScoreSyncService>();
            var resultado = await sync.SincronizarFifaAsync(stoppingToken);

            if (resultado.Actualizados > 0)
            {
                _logger.LogInformation(
                    "Marcadores en vivo sincronizados: {Procesados} procesados, {Actualizados} actualizados.",
                    resultado.Procesados,
                    resultado.Actualizados);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar marcadores en vivo desde FIFA.");
        }
    }
}
