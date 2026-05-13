namespace WorldCup.Api.DTOs
{
    public class ActualizarMarcadorDTO
    {
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }
        public bool Finalizado { get; set; }
        public string Estado { get; set; } = "Finalizado";
        public int? AdminUsuarioId { get; set; }
    }
}
