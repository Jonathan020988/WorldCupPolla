namespace WorldCup.Api.DTOs
{
    public class CrearInvitacionPollaDto
    {
        public int RemitenteId { get; set; }
        public string EmailInvitado { get; set; } = "";
        public string LinkInvitacion { get; set; } = "";
    }

    public class NotificacionDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "";
        public int? PollaId { get; set; }
        public string PollaNombre { get; set; } = "";
        public int? UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public int? PartidoId { get; set; }
        public string Partido { get; set; } = "";
        public string Estado { get; set; } = "Pendiente";
        public string Mensaje { get; set; } = "";
        public string Link { get; set; } = "";
        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaPartido { get; set; }
        public bool RequiereAccion { get; set; }
        public string Celular { get; set; } = "";
        public int? CantidadUsuarios { get; set; }
        public string PlanNombre { get; set; } = "";
        public decimal? ValorPlan { get; set; }
        public string CodigoHabilitacion { get; set; } = "";
    }
}
