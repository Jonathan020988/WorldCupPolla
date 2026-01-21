using System.Net.Http.Json;
using WorldCup.App.Shared.Models;

namespace WorldCup.App.Shared.Services;

public class PartidosService
{
    private readonly HttpClient _http;

    public PartidosService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PartidoDto>> GetPartidosAsync()
    {
        var result = await _http.GetFromJsonAsync<List<PartidoDto>>("api/Partidos");
        return result ?? new List<PartidoDto>();
    }
}
