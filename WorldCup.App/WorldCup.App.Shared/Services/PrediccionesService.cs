using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;
using WorldCup.App.Shared.Models;

namespace WorldCup.App.Shared.Services;

public class PrediccionesService
{
    private readonly HttpClient _http;

    public PrediccionesService(HttpClient http)
    {
        _http = http;
    }

    public async Task GuardarMultiplesAsync(GuardarPrediccionGrupoDto dto)
    {
        var response = await _http.PostAsJsonAsync(
            "api/Predicciones/guardar-multiples",
            dto
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task<List<PrediccionExistenteDto>> GetPrediccionesAsync(
        int pollaId,
        int usuarioId)
    {
        var response = await _http.GetAsync(
            $"api/Predicciones?pollaId={pollaId}&usuarioId={usuarioId}"
        );

        if (!response.IsSuccessStatusCode)
        {
            return new List<PrediccionExistenteDto>();
        }

        var data = await response.Content
            .ReadFromJsonAsync<List<PrediccionExistenteDto>>();

        return data ?? new List<PrediccionExistenteDto>();
    }

    public async Task EliminarPrediccionAsync(
        int pollaId,
        int usuarioId,
        int partidoId)
    {
        var response = await _http.DeleteAsync(
            $"api/Predicciones/{partidoId}?pollaId={pollaId}&usuarioId={usuarioId}"
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }
}
