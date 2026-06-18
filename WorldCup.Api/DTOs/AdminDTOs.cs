namespace WorldCup.Api.DTOs
{
    public class AdminActualizarPrediccionDTO
    {
        public int AdminUsuarioId { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public int PartidoId { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
    }

    public class AdminActualizarPartidoDTO
    {
        public int AdminUsuarioId { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool TiempoExtra { get; set; }
        public int? ClasificadoId { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
    }

    public class AdminActualizarFechaPartidoDTO
    {
        public int AdminUsuarioId { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class AdminActualizarUsuarioEstadoDTO
    {
        public int AdminUsuarioId { get; set; }
        public bool Activo { get; set; }
    }

    public class AdminFaseTorneoDTO
    {
        public int AdminUsuarioId { get; set; }
    }

    public class AdminActualizarReaperturaDTO
    {
        public int AdminUsuarioId { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public int? PartidoId { get; set; }
        public string Fase { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool Activa { get; set; }
    }

    public class AdminEnviarAlertaPendientesDTO
    {
        public int AdminUsuarioId { get; set; }
        public int PollaId { get; set; }
        public string TipoAlerta { get; set; } = "Todo";
        public int? PartidoId { get; set; }
    }

    public class AdminEnviarAlertaMasivaPendientesDTO
    {
        public int AdminUsuarioId { get; set; }
        public int? PollaId { get; set; }
        public string TipoAlerta { get; set; } = "Todo";
        public string FaseMarcadores { get; set; } = "";
        public int? PartidoId { get; set; }
        public bool EnviarCorreo { get; set; } = true;
    }

    public class AdminEnviarAlertaContactoCuposDTO
    {
        public int AdminUsuarioId { get; set; }
        public string Mensaje { get; set; } = "";
    }
}
