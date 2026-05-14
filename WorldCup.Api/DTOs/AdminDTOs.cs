namespace WorldCup.Api.DTOs
{
    public class AdminActualizarPrediccionDTO
    {
        public int AdminUsuarioId { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
    }

    public class AdminActualizarPartidoDTO
    {
        public int AdminUsuarioId { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
    }

    public class AdminActualizarUsuarioEstadoDTO
    {
        public int AdminUsuarioId { get; set; }
        public bool Activo { get; set; }
    }
}
