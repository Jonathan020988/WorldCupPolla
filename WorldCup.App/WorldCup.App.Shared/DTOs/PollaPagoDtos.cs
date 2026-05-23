namespace WorldCup.App.Shared.DTOs
{
    public class PollaPagoParticipanteDto
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public decimal ValorAPagar { get; set; }
        public decimal AbonoPagado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string EstadoPago { get; set; } = "";
        public string NotaPago { get; set; } = "";
        public DateTime? PagoActualizadoEn { get; set; }
        public DateTime? PagoNotificadoEn { get; set; }
    }

    public class PollaPagosResumenDto
    {
        public int PollaId { get; set; }
        public decimal ValorBase { get; set; }
        public int TotalParticipantes { get; set; }
        public decimal TotalEsperado { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal TotalPendiente { get; set; }
        public int ParticipantesPagados { get; set; }
        public int ParticipantesConAbono { get; set; }
        public int ParticipantesSinPago { get; set; }
        public List<PollaPagoParticipanteDto> Participantes { get; set; } = new();
    }

    public class ActualizarPagoParticipanteDto
    {
        public int SolicitanteId { get; set; }
        public decimal ValorAPagar { get; set; }
        public decimal AbonoPagado { get; set; }
        public string? NotaPago { get; set; }
    }

    public class NotificarPagoPendienteDto
    {
        public int SolicitanteId { get; set; }
    }
}
