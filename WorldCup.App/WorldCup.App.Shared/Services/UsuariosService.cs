using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http.Json;
using WorldCup.App.Shared.DTOs;
using WorldCup.App.Shared.Services;


namespace WorldCup.App.Shared.Services
{
    public class UsuariosService
    {
        private readonly HttpClient _http;

        public UsuariosService(HttpClient http)
        {
            _http = http;
        }

        public async Task RegistrarUsuarioAsync(RegistroUsuarioDTO dto)
        {
            var response = await _http.PostAsJsonAsync("api/Usuarios/registro", dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<string?> ConfirmarCodigoCorreoAsync(ConfirmarCodigoCorreoDTO dto)
        {
            var response = await _http.PostAsJsonAsync("api/Usuarios/confirmar-codigo", dto);

            if (!response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }

        public async Task<string?> ConfirmarCorreoAsync(string token)
        {
            var response = await _http.GetAsync(
                $"api/Usuarios/confirmar-correo?token={Uri.EscapeDataString(token)}");

            if (!response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }
    }
}
