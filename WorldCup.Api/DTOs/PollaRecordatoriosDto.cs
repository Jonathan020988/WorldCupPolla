namespace WorldCup.App.Shared.DTOs
{
    public class PollaRecordatorioPartidoDto
    {
        public int PartidoId { get; set; }
        public string Fase { get; set; } = "";
        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int TotalPendientes { get; set; }
    }

    public class PollaRecordatorioUsuarioDto
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public bool AlertaPendiente { get; set; }
    }

    public class EnviarRecordatorioPollaDto
    {
        public int SolicitanteId { get; set; }
    }

    public class ResultadoRecordatorioPollaDto
    {
        public string Mensaje { get; set; } = "";
        public int TotalEnviados { get; set; }
        public int CorreosEnviados { get; set; }
        public int CorreosFallidos { get; set; }
    }
}
