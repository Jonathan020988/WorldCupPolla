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
        public int? PrediceClasificadoId { get; set; }
        public bool PrediceTiempoExtra { get; set; }
        public bool PredicePenales { get; set; }
    }

    public class AdminGuardarPodioUsuarioDTO
    {
        public int AdminUsuarioId { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public int CampeonId { get; set; }
        public int SubcampeonId { get; set; }
        public int TerceroId { get; set; }
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

    public class AdminPublicarRankingPartidoDTO
    {
        public int AdminUsuarioId { get; set; }
        public int? PollaId { get; set; }
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

    public class AdminCrucesFaseDTO
    {
        public string FaseOrigen { get; set; } = "";
        public string SiguienteFase { get; set; } = "";
        public bool PuedePublicar { get; set; }
        public bool YaGenerada { get; set; }
        public string Mensaje { get; set; } = "";
        public List<AdminCrucePartidoDTO> Cruces { get; set; } = new();
        public List<AdminEquipoCruceDTO> EquiposDisponibles { get; set; } = new();
    }

    public class AdminCrucePartidoDTO
    {
        public int NumeroPartido { get; set; }
        public string Fase { get; set; } = "";
        public DateTime Fecha { get; set; }
        public int LocalId { get; set; }
        public string Local { get; set; } = "";
        public int VisitanteId { get; set; }
        public string Visitante { get; set; } = "";
        public string OrigenLocal { get; set; } = "";
        public string OrigenVisitante { get; set; } = "";
    }

    public class AdminEquipoCruceDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Grupo { get; set; } = "";
    }

    public class AdminPublicarCrucesDTO
    {
        public int AdminUsuarioId { get; set; }
        public List<AdminCrucePartidoDTO> Cruces { get; set; } = new();
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

    public class AdminEnviarNotificacionGlobalDTO
    {
        public int AdminUsuarioId { get; set; }
        public int? UsuarioId { get; set; }
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
    }

    public class AdminComunicadoEntregaDTO
    {
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public string UsuarioEmail { get; set; } = "";
        public bool NotificacionGuardada { get; set; }
        public bool CorreoEnviado { get; set; }
        public string Detalle { get; set; } = "";
    }
}
