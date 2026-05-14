namespace WorldCup.App.Shared.DTOs
{
    public class PartidoEliminacionDto
    {
        public int NumeroPartido { get; set; }

        public string Local { get; set; } = string.Empty;

        public string Visitante { get; set; } = string.Empty;

        public string GrupoLocal { get; set; } = string.Empty;

        public string GrupoVisitante { get; set; } = string.Empty;

        public List<string> GruposTerceroPermitidos { get; set; } = new();

        public int GolesLocal { get; set; }

        public int GolesVisitante { get; set; }

        public string? GanadorPenales { get; set; }
    }
}
