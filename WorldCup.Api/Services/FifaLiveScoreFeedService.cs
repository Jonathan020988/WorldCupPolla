using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldCup.Api.Services;

public class FifaLiveScoreFeedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FifaLiveScoreFeedService> _logger;

    public FifaLiveScoreFeedService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<FifaLiveScoreFeedService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarcadorEnVivoFuente>> ObtenerMarcadoresAsync(
        CancellationToken cancellationToken = default)
    {
        var url = ObtenerUrl();
        var response = await _http.GetFromJsonAsync<FifaMatchesResponse>(
            url,
            JsonOptions,
            cancellationToken);

        if (response?.Results == null)
        {
            return Array.Empty<MarcadorEnVivoFuente>();
        }

        return response.Results
            .Where(m => m.MatchNumber.HasValue || !string.IsNullOrWhiteSpace(m.IdMatch))
            .Select(Convertir)
            .ToList();
    }

    private string ObtenerUrl()
    {
        var configurada = _configuration["MarcadoresEnVivo:Fifa:Url"];
        if (!string.IsNullOrWhiteSpace(configurada))
        {
            return configurada;
        }

        var idioma = _configuration["MarcadoresEnVivo:Fifa:Idioma"] ?? "es";
        var idCompeticion = _configuration["MarcadoresEnVivo:Fifa:IdCompeticion"] ?? "17";
        var idTemporada = _configuration["MarcadoresEnVivo:Fifa:IdTemporada"] ?? "285023";
        var cantidad = _configuration.GetValue<int?>("MarcadoresEnVivo:Fifa:Cantidad") ?? 150;

        return "https://api.fifa.com/api/v3/calendar/matches" +
            $"?language={Uri.EscapeDataString(idioma)}" +
            $"&count={cantidad}" +
            $"&idCompetition={Uri.EscapeDataString(idCompeticion)}" +
            $"&idSeason={Uri.EscapeDataString(idTemporada)}";
    }

    private MarcadorEnVivoFuente Convertir(FifaMatch match)
    {
        var golesLocal = match.HomeTeamScore ?? match.Home?.Score;
        var golesVisitante = match.AwayTeamScore ?? match.Away?.Score;
        var estado = NormalizarEstado(match, golesLocal, golesVisitante);

        return new MarcadorEnVivoFuente
        {
            Fuente = "FIFA",
            IdExterno = match.IdMatch,
            NumeroPartido = match.MatchNumber,
            CodigoLocal = NormalizarCodigo(match.Home?.Abbreviation) ??
                NormalizarCodigo(match.Home?.IdAssociation) ??
                NormalizarCodigo(match.Home?.IdCountry),
            CodigoVisitante = NormalizarCodigo(match.Away?.Abbreviation) ??
                NormalizarCodigo(match.Away?.IdAssociation) ??
                NormalizarCodigo(match.Away?.IdCountry),
            GolesLocal = golesLocal,
            GolesVisitante = golesVisitante,
            Estado = estado,
            Minuto = string.IsNullOrWhiteSpace(match.MatchTime)
                ? null
                : match.MatchTime.Trim(),
            FechaUtc = ParsearFechaUtc(match.Date)
        };
    }

    private static string NormalizarEstado(
        FifaMatch match,
        int? golesLocal,
        int? golesVisitante)
    {
        if (match.MatchStatus == 0)
        {
            return "Finalizado";
        }

        if (match.MatchStatus == 1)
        {
            return "Programado";
        }

        if (golesLocal.HasValue &&
            golesVisitante.HasValue &&
            !string.IsNullOrWhiteSpace(match.MatchTime))
        {
            return "EnJuego";
        }

        return "SinDatos";
    }

    private static string? NormalizarCodigo(string? codigo)
    {
        return string.IsNullOrWhiteSpace(codigo)
            ? null
            : codigo.Trim().ToUpperInvariant();
    }

    private DateTime? ParsearFechaUtc(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(fecha, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        _logger.LogDebug("No se pudo interpretar la fecha FIFA {Fecha}", fecha);
        return null;
    }

    private sealed class FifaMatchesResponse
    {
        [JsonPropertyName("Results")]
        public List<FifaMatch> Results { get; set; } = new();
    }

    private sealed class FifaMatch
    {
        [JsonPropertyName("IdMatch")]
        public string? IdMatch { get; set; }

        [JsonPropertyName("MatchNumber")]
        public int? MatchNumber { get; set; }

        [JsonPropertyName("Date")]
        public string? Date { get; set; }

        [JsonPropertyName("MatchTime")]
        public string? MatchTime { get; set; }

        [JsonPropertyName("MatchStatus")]
        public int? MatchStatus { get; set; }

        [JsonPropertyName("HomeTeamScore")]
        public int? HomeTeamScore { get; set; }

        [JsonPropertyName("AwayTeamScore")]
        public int? AwayTeamScore { get; set; }

        [JsonPropertyName("Home")]
        public FifaTeam? Home { get; set; }

        [JsonPropertyName("Away")]
        public FifaTeam? Away { get; set; }
    }

    private sealed class FifaTeam
    {
        [JsonPropertyName("Score")]
        public int? Score { get; set; }

        [JsonPropertyName("Abbreviation")]
        public string? Abbreviation { get; set; }

        [JsonPropertyName("IdCountry")]
        public string? IdCountry { get; set; }

        [JsonPropertyName("IdAssociation")]
        public string? IdAssociation { get; set; }
    }
}

public class MarcadorEnVivoFuente
{
    public string Fuente { get; set; } = "";
    public string? IdExterno { get; set; }
    public int? NumeroPartido { get; set; }
    public string? CodigoLocal { get; set; }
    public string? CodigoVisitante { get; set; }
    public int? GolesLocal { get; set; }
    public int? GolesVisitante { get; set; }
    public string Estado { get; set; } = "SinDatos";
    public string? Minuto { get; set; }
    public DateTime? FechaUtc { get; set; }
}
