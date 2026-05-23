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

        public async Task<PollaDto?> GetPollaAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<PollaDto>(
                $"api/Polla/{pollaId}"
            );
        }

        public async Task<List<RankingPollaDto>> GetRankingAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<List<RankingPollaDto>>(
                $"api/Polla/{pollaId}/ranking"
            ) ?? new();
        }

        public async Task<List<ParticipanteDto>> GetParticipantesAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<List<ParticipanteDto>>(
                $"api/Polla/{pollaId}/participantes"
            ) ?? new();
        }

        public async Task<PollaPagosResumenDto?> GetControlPagosAsync(
            int pollaId,
            int solicitanteId)
        {
            return await _http.GetFromJsonAsync<PollaPagosResumenDto>(
                $"api/Polla/{pollaId}/pagos?solicitanteId={solicitanteId}"
            );
        }

        public async Task<PollaPagoParticipanteDto> ActualizarPagoParticipanteAsync(
            int pollaId,
            int usuarioId,
            ActualizarPagoParticipanteDto dto)
        {
            var response = await _http.PutAsJsonAsync(
                $"api/Polla/{pollaId}/pagos/{usuarioId}",
                dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            return await response.Content.ReadFromJsonAsync<PollaPagoParticipanteDto>()
                ?? new PollaPagoParticipanteDto();
        }

        public async Task<string> NotificarPagoPendienteAsync(
            int pollaId,
            int usuarioId,
            NotificarPagoPendienteDto dto)
        {
            var response = await _http.PostAsJsonAsync(
                $"api/Polla/{pollaId}/pagos/{usuarioId}/notificar",
                dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            var result = await response.Content.ReadFromJsonAsync<MensajeRespuestaDto>();
            return result?.Mensaje ?? "Aviso enviado.";
        }

        public async Task InvitarUsuarioAsync(int pollaId, int usuarioId)
        {
            await _http.PostAsync(
                $"api/Polla/{pollaId}/invitar/{usuarioId}",
                null
            );
        }

        public async Task<string> CrearInvitacionAsync(
            int pollaId,
            CrearInvitacionPollaDto dto)
        {
            var response = await _http.PostAsJsonAsync(
                $"api/Polla/{pollaId}/invitaciones",
                dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            var data = await response.Content.ReadFromJsonAsync<InvitacionCreadaDto>();
            return data?.LinkInvitacion ?? dto.LinkInvitacion;
        }

        public async Task CrearPollaAsync(CrearPollaDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/Polla", dto);
            response.EnsureSuccessStatusCode();
        }

        public async Task ActualizarPollaAsync(int pollaId, CrearPollaDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/Polla/{pollaId}", dto);
            response.EnsureSuccessStatusCode();
        }

        public async Task ActualizarNombrePollaAsync(
            int pollaId,
            int solicitanteId,
            string nombre)
        {
            var response = await _http.PutAsJsonAsync(
                $"api/Polla/{pollaId}/nombre",
                new
                {
                    Nombre = nombre,
                    SolicitanteId = solicitanteId
                });

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }
        }

        public async Task EliminarPollaAsync(int pollaId, int solicitanteId)
        {
            var response = await _http.DeleteAsync($"api/Polla/{pollaId}?solicitanteId={solicitanteId}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }
        }

        public async Task ActualizarPinAsync(int pollaId, string pin)
        {
            var dto = new
            {
                PinIngreso = pin
            };

            var response = await _http.PutAsJsonAsync(
                $"api/Polla/{pollaId}/pin",
                dto
            );

            response.EnsureSuccessStatusCode();
        }

        public async Task EliminarMiembroAsync(int pollaId, int usuarioId, int solicitanteId)
        {
            var response = await _http.DeleteAsync(
                $"api/Polla/{pollaId}/miembros/{usuarioId}?solicitanteId={solicitanteId}"
            );

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<SolicitudIngresoDto>> GetSolicitudesParaCreadorAsync(int creadorId)
        {
            return await _http.GetFromJsonAsync<List<SolicitudIngresoDto>>(
                $"api/Polla/solicitudes/{creadorId}"
            ) ?? new();
        }

        public async Task<List<NotificacionDto>> GetNotificacionesAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<List<NotificacionDto>>(
                $"api/Polla/notificaciones/{usuarioId}"
            ) ?? new();
        }

        public async Task AprobarSolicitudAsync(int solicitudId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/solicitudes/{solicitudId}/aprobar",
                null);

            response.EnsureSuccessStatusCode();

        }

        public async Task RechazarSolicitudAsync(int solicitudId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/solicitudes/{solicitudId}/rechazar",
                null);

            response.EnsureSuccessStatusCode();

        }

        public async Task EliminarSolicitudAsync(int solicitudId)
        {
            var response = await _http.DeleteAsync(
                $"api/Polla/solicitudes/{solicitudId}");

            response.EnsureSuccessStatusCode();

        }

        public async Task AceptarInvitacionAsync(int invitacionId, int usuarioId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/invitaciones/{invitacionId}/aceptar?usuarioId={usuarioId}",
                null);

            response.EnsureSuccessStatusCode();
        }

        public async Task RechazarInvitacionAsync(int invitacionId, int usuarioId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/invitaciones/{invitacionId}/rechazar?usuarioId={usuarioId}",
                null);

            response.EnsureSuccessStatusCode();
        }

        private class InvitacionCreadaDto
        {
            public string LinkInvitacion { get; set; } = "";
        }

        private class MensajeRespuestaDto
        {
            public string Mensaje { get; set; } = "";
        }

        public async Task<List<DetalleRankingDto>> GetRankingDetalleAsync(int pollaId)
        {
            return await _http.GetFromJsonAsync<List<DetalleRankingDto>>(
                $"api/Polla/{pollaId}/ranking-detalle"
            ) ?? new();
        }



    }
}
