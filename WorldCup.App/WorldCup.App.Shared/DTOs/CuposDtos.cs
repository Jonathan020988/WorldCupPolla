namespace WorldCup.App.Shared.DTOs
{
    public class CuposUsuarioDto
    {
        public int UsuarioId { get; set; }
        public int MaximoMiembrosPorPolla { get; set; }
        public bool CuposIlimitados { get; set; }
        public int CupoBase { get; set; }
        public int? SolicitudPendienteId { get; set; }
        public string SolicitudPendienteEstado { get; set; } = "";
        public string CodigoPendiente { get; set; } = "";
    }

    public class SolicitarAmpliacionCuposDto
    {
        public int UsuarioId { get; set; }
        public string Celular { get; set; } = "";
        public int CantidadUsuarios { get; set; }
    }

    public class ActivarCuposDto
    {
        public int UsuarioId { get; set; }
        public string Codigo { get; set; } = "";
    }
}
