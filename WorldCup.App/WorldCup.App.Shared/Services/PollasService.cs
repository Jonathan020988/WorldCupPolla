using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;

namespace WorldCup.App.Shared.Services
{
    public class PollasService
    {
        private readonly HttpClient _http;

        public PollasService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PollaDto>> GetMisPollasAsync()
        {
            return await _http.GetFromJsonAsync<List<PollaDto>>(
                "api/Polla"
            ) ?? new();
        }

        public async Task<List<PollaDto>> GetPollasPorUsuarioAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<List<PollaDto>>(
                $"api/Polla/usuario/{usuarioId}"
            ) ?? new();
        }


    }
}
