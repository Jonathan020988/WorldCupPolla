namespace WorldCup.App.Shared.Models
{
    public class PartidoDto
    {
        public int Id { get; set; }
        public string Fase { get; set; } = "";
        public int LocalId { get; set; }
        public int VisitanteId { get; set; }
        public string Local { get; set; } = "";
        public string Visitante { get; set; } = "";
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool TiempoExtra { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
        public bool Finalizado { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime Fecha { get; set; }
        public string? Grupo { get; set; }
    }
}
