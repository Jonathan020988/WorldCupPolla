namespace WorldCup.App.Shared.DTOs
{
    public class PartidoEliminacionDto
    {
        public string Local { get; set; } = string.Empty;

        public string Visitante { get; set; } = string.Empty;

        public int GolesLocal { get; set; }

        public int GolesVisitante { get; set; }

        public string? GanadorPenales { get; set; }
    }
}