using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services;

public class LiveScoreSyncService
{
    private readonly AppDbContext _context;
    private readonly FifaLiveScoreFeedService _fifaFeed;
    private readonly ILogger<LiveScoreSyncService> _logger;

    public LiveScoreSyncService(
        AppDbContext context,
        FifaLiveScoreFeedService fifaFeed,
        ILogger<LiveScoreSyncService> logger)
    {
        _context = context;
        _fifaFeed = fifaFeed;
        _logger = logger;
    }

    public async Task<LiveScoreSyncResult> SincronizarFifaAsync(
        CancellationToken cancellationToken = default)
    {
        var marcadores = await _fifaFeed.ObtenerMarcadoresAsync(cancellationToken);
        var partidos = await _context.Partidos
            .Include(p => p.Local)
            .Include(p => p.Visitante)
            .ToListAsync(cancellationToken);

        var porNumero = partidos
            .Where(p => p.NumeroPartidoFifa.HasValue)
            .GroupBy(p => p.NumeroPartidoFifa!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var procesados = 0;
        var actualizados = 0;
        var sinCoincidencia = 0;
        var ahora = DateTime.UtcNow;

        foreach (var marcador in marcadores)
        {
            var partido = BuscarPartido(marcador, partidos, porNumero);
            if (partido == null)
            {
                sinCoincidencia++;
                continue;
            }

            procesados++;

            if (AplicarMarcador(partido, marcador, ahora))
            {
                actualizados++;
            }
        }

        if (actualizados > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new LiveScoreSyncResult
        {
            Leidos = marcadores.Count,
            Procesados = procesados,
            Actualizados = actualizados,
            SinCoincidencia = sinCoincidencia
        };
    }

    private Partido? BuscarPartido(
        MarcadorEnVivoFuente marcador,
        List<Partido> partidos,
        Dictionary<int, Partido> porNumero)
    {
        if (marcador.NumeroPartido.HasValue)
        {
            if (porNumero.TryGetValue(marcador.NumeroPartido.Value, out var porNumeroFifa))
            {
                return porNumeroFifa;
            }

            var porId = partidos.FirstOrDefault(p => p.Id == marcador.NumeroPartido.Value);
            if (porId != null)
            {
                return porId;
            }
        }

        if (!string.IsNullOrWhiteSpace(marcador.CodigoLocal) &&
            !string.IsNullOrWhiteSpace(marcador.CodigoVisitante))
        {
            var porEquipos = partidos.FirstOrDefault(p =>
                CodigosCoinciden(p.Local.CodigoFifa, marcador.CodigoLocal) &&
                CodigosCoinciden(p.Visitante.CodigoFifa, marcador.CodigoVisitante));

            if (porEquipos != null)
            {
                return porEquipos;
            }
        }

        if (marcador.FechaUtc.HasValue &&
            !string.IsNullOrWhiteSpace(marcador.CodigoLocal) &&
            !string.IsNullOrWhiteSpace(marcador.CodigoVisitante))
        {
            return partidos
                .Where(p =>
                    Math.Abs((p.Fecha - marcador.FechaUtc.Value).TotalHours) <= 8 &&
                    CodigosCoinciden(p.Local.CodigoFifa, marcador.CodigoLocal) &&
                    CodigosCoinciden(p.Visitante.CodigoFifa, marcador.CodigoVisitante))
                .OrderBy(p => Math.Abs((p.Fecha - marcador.FechaUtc.Value).TotalMinutes))
                .FirstOrDefault();
        }

        return null;
    }

    private bool AplicarMarcador(
        Partido partido,
        MarcadorEnVivoFuente marcador,
        DateTime actualizadoEn)
    {
        var cambio = false;

        cambio |= CambiarSiDiferente(
            partido.NumeroPartidoFifa,
            marcador.NumeroPartido,
            v => partido.NumeroPartidoFifa = v);
        cambio |= CambiarSiDiferente(
            partido.MarcadorEnVivoLocal,
            marcador.GolesLocal,
            v => partido.MarcadorEnVivoLocal = v);
        cambio |= CambiarSiDiferente(
            partido.MarcadorEnVivoVisitante,
            marcador.GolesVisitante,
            v => partido.MarcadorEnVivoVisitante = v);
        cambio |= CambiarSiDiferente(
            partido.EstadoMarcadorEnVivo,
            marcador.Estado,
            v => partido.EstadoMarcadorEnVivo = v);
        cambio |= CambiarSiDiferente(
            partido.MinutoMarcadorEnVivo,
            marcador.Minuto,
            v => partido.MinutoMarcadorEnVivo = v);
        cambio |= CambiarSiDiferente(
            partido.FuenteMarcadorEnVivo,
            marcador.Fuente,
            v => partido.FuenteMarcadorEnVivo = v);
        cambio |= CambiarSiDiferente(
            partido.IdExternoMarcadorEnVivo,
            marcador.IdExterno,
            v => partido.IdExternoMarcadorEnVivo = v);

        if (cambio)
        {
            partido.MarcadorEnVivoActualizadoEn = actualizadoEn;
        }

        return cambio;
    }

    private static bool CambiarSiDiferente<T>(
        T valorActual,
        T valorNuevo,
        Action<T> asignar)
    {
        if (EqualityComparer<T>.Default.Equals(valorActual, valorNuevo))
        {
            return false;
        }

        asignar(valorNuevo);
        return true;
    }

    private static bool CodigosCoinciden(string? local, string? fuente)
    {
        return string.Equals(
            NormalizarCodigo(local),
            NormalizarCodigo(fuente),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizarCodigo(string? codigo)
    {
        return string.IsNullOrWhiteSpace(codigo)
            ? null
            : codigo.Trim().ToUpperInvariant();
    }
}

public class LiveScoreSyncResult
{
    public int Leidos { get; set; }
    public int Procesados { get; set; }
    public int Actualizados { get; set; }
    public int SinCoincidencia { get; set; }
}
