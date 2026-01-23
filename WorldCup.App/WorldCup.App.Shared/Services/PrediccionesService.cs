using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;

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
}
