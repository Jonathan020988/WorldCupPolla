using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;

namespace WorldCup.App.Shared.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;

        public AuthService(HttpClient http)
        {
            _http = http;
        }

        public async Task<UsuarioDto?> LoginAsync(LoginDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", dto);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<UsuarioDto>();
        }

        public async Task<string?> SolicitarResetPasswordAsync(SolicitarResetPasswordDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/auth/olvide-password", dto);

            if (response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string?> RestablecerPasswordAsync(RestablecerPasswordDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/auth/restablecer-password", dto);

            if (response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }
    }
}
