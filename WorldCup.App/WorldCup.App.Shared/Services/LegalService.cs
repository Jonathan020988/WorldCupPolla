using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;

namespace WorldCup.App.Shared.Services
{
    public class LegalService
    {
        public const string VersionActual = "2026-06-03";
        private readonly HttpClient _http;

        public LegalService(HttpClient http)
        {
            _http = http;
        }

        public async Task<LegalConsentStatusDto?> GetEstadoAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<LegalConsentStatusDto>(
                $"api/Legal/estado/{usuarioId}");
        }

        public async Task<string?> AceptarAsync(AceptarLegalDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/Legal/aceptar", dto);

            if (response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }
    }
}
