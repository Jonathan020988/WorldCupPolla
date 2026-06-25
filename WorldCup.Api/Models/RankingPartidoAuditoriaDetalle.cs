namespace WorldCup.Api.Models
{
    public class RankingPartidoAuditoriaDetalle
    {
        public long Id { get; set; }
        public int PartidoId { get; set; }
        public Partido Partido { get; set; } = null!;
        public int PollaId { get; set; }
        public Polla Polla { get; set; } = null!;
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;
        public bool TienePrediccion { get; set; }
        public int? GolesLocalPrediccion { get; set; }
        public int? GolesVisitantePrediccion { get; set; }
        public int PuntosPrevios { get; set; }
        public int PuntosCambio { get; set; }
        public int PuntosRanking { get; set; }
        public int PuntosMarcadorCierre { get; set; }
        public int PuntosClasificacionCierre { get; set; }
        public int PuntosPodioCierre { get; set; }
        public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;
    }
}
