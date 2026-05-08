namespace WorldCup.App.Shared.DTOs
{
    public class SimuladorPartidoDto
    {
        public int Id { get; set; }

        public string Grupo { get; set; } = string.Empty;

        public string Local { get; set; } = string.Empty;

        public string Visitante { get; set; } = string.Empty;

        public int? GolesLocal { get; set; }

        public int? GolesVisitante { get; set; }
    }
}