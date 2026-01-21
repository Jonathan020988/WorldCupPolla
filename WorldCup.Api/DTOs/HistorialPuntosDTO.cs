namespace WorldCup.Api.DTOs
{
    public class HistorialPuntosDTO
    {
        public int PartidoId { get; set; }
        public DateTime Fecha { get; set; }
        public string Fase { get; set; } = string.Empty;
        public string Partido { get; set; } = string.Empty;

        public int PuntosPartido { get; set; }
        public int PuntosAcumulados { get; set; }
    }
}
