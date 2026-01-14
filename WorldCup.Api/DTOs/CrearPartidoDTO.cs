namespace WorldCup.Api.DTOs
{
    public class CrearPartidoDTO
    {
        public DateTime Fecha { get; set; }
        public string Fase { get; set; } = null!;
        public int LocalId { get; set; }
        public int VisitanteId { get; set; }
    }
}
