namespace WorldCup.App.Shared.DTOs
{
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
    }
}
