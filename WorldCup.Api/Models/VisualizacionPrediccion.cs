namespace WorldCup.Api.Models
{
    public class VisualizacionPrediccion
    {
        public long Id { get; set; }
        public int PollaId { get; set; }
        public Polla Polla { get; set; } = null!;
        public int UsuarioObjetivoId { get; set; }
        public Usuario UsuarioObjetivo { get; set; } = null!;
        public int PartidoId { get; set; }
        public Partido Partido { get; set; } = null!;
        public int UsuarioVisualizadorId { get; set; }
        public Usuario UsuarioVisualizador { get; set; } = null!;
        public DateTime FechaVisualizacion { get; set; } = DateTime.UtcNow;
    }
}
