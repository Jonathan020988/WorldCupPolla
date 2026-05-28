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

        public async Task<PollaDto?> GetPollaAsync(
            int pollaId,
            int solicitanteId)
        {
            return await LeerRespuestaAsync<PollaDto>(
                $"api/Polla/{pollaId}?solicitanteId={solicitanteId}"
            );
        }

        public async Task<PollaDto?> GetPollaPublicaAsync(int pollaId)
        {
            return await LeerRespuestaAsync<PollaDto>(
                $"api/Polla/{pollaId}/publica"
            );
        }

        public async Task<CuposUsuarioDto?> GetCuposUsuarioAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<CuposUsuarioDto>(
                $"api/Polla/cupos/{usuarioId}"
            );
        }

        public async Task<string> SolicitarAmpliacionCuposAsync(SolicitarAmpliacionCuposDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/Polla/cupos/solicitudes", dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            var result = await response.Content.ReadFromJsonAsync<MensajeRespuestaDto>();
            return result?.Mensaje ?? "Solicitud enviada al administrador.";
        }

        public async Task<string> ActivarCuposAsync(ActivarCuposDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/Polla/cupos/activar", dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            var result = await response.Content.ReadFromJsonAsync<MensajeRespuestaDto>();
            return result?.Mensaje ?? "Felicitaciones, has habilitado la opción de agregar más usuarios a tus pollas.";
        }

        public async Task<List<RankingPollaDto>> GetRankingAsync(
            int pollaId,
            int? solicitanteId = null)
        {
            var url = $"api/Polla/{pollaId}/ranking";
            if (solicitanteId.HasValue)
                url += $"?solicitanteId={solicitanteId.Value}";

            return await LeerRespuestaAsync<List<RankingPollaDto>>(
                url
            ) ?? new();
        }

        public async Task<List<ParticipanteDto>> GetParticipantesAsync(
            int pollaId,
            int? solicitanteId = null)
        {
            var url = $"api/Polla/{pollaId}/participantes";
            if (solicitanteId.HasValue)
                url += $"?solicitanteId={solicitanteId.Value}";

            return await LeerRespuestaAsync<List<ParticipanteDto>>(
                url
            ) ?? new();
        }

        public async Task<ParticipanteDto> ActualizarObservacionParticipanteAsync(
            int pollaId,
            int usuarioId,
            ActualizarObservacionParticipanteDto dto)
        {
            var response = await _http.PutAsJsonAsync(
                $"api/Polla/{pollaId}/participantes/{usuarioId}/observacion",
                dto);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }

            return await response.Content.ReadFromJsonAsync<ParticipanteDto>()
                ?? new ParticipanteDto();
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

        public async Task InvitarUsuarioAsync(
            int pollaId,
            int usuarioId,
            int solicitanteId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/{pollaId}/invitar/{usuarioId}?solicitanteId={solicitanteId}",
                null
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
            }
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

        public async Task ActualizarPinAsync(
            int pollaId,
            string pin,
            int solicitanteId)
        {
            var dto = new
            {
                PinIngreso = pin,
                SolicitanteId = solicitanteId
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

        public async Task<List<AlertaUsuarioDto>> GetAlertasPendientesAsync(int usuarioId)
        {
            return await _http.GetFromJsonAsync<List<AlertaUsuarioDto>>(
                $"api/Polla/alertas/{usuarioId}"
            ) ?? new();
        }

        public async Task CerrarAlertaUsuarioAsync(int alertaId, int usuarioId)
        {
            var response = await _http.PostAsync(
                $"api/Polla/alertas/{alertaId}/cerrar?usuarioId={usuarioId}",
                null);

            response.EnsureSuccessStatusCode();
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

        public async Task<List<DetalleRankingDto>> GetRankingDetalleAsync(
            int pollaId,
            int solicitanteId)
        {
            return await LeerRespuestaAsync<List<DetalleRankingDto>>(
                $"api/Polla/{pollaId}/ranking-detalle?solicitanteId={solicitanteId}"
            ) ?? new();
        }

        private async Task<T?> LeerRespuestaAsync<T>(string url)
        {
            var response = await _http.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                var mensaje = await response.Content.ReadAsStringAsync();
                throw new Exception(string.IsNullOrWhiteSpace(mensaje)
                    ? "No tienes permisos para ver esta información."
                    : mensaje);
            }

            return await response.Content.ReadFromJsonAsync<T>();
        }



    }
}
