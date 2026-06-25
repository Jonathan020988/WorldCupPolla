namespace WorldCup.Api.Models
{
    public class RankingPartidoPublicacion
    {
        public int Id { get; set; }
        public int PartidoId { get; set; }
        public Partido Partido { get; set; } = null!;
        public bool Publicado { get; set; }
        public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;
        public DateTime? FechaPublicacion { get; set; }
        public int? AdminCalculoId { get; set; }
        public int? AdminPublicacionId { get; set; }
    }
}
