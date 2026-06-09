namespace WorldCup.App.Shared.DTOs
{
    public class SimuladorPartidoDto
    {
        public int Id { get; set; }

        public string Grupo { get; set; } = string.Empty;

        public int LocalId { get; set; }

        public string Local { get; set; } = string.Empty;

        public int VisitanteId { get; set; }

        public string Visitante { get; set; } = string.Empty;

        public int? GolesLocal { get; set; }

        public int? GolesVisitante { get; set; }
    }
}
