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
        public int? NumeroPartidoFifa { get; set; }
        public int? MarcadorEnVivoLocal { get; set; }
        public int? MarcadorEnVivoVisitante { get; set; }
        public string? EstadoMarcadorEnVivo { get; set; }
        public string? MinutoMarcadorEnVivo { get; set; }
        public DateTime? MarcadorEnVivoActualizadoEn { get; set; }
        public string? FuenteMarcadorEnVivo { get; set; }
        public string? IdExternoMarcadorEnVivo { get; set; }
        public bool TiempoExtra { get; set; }
        public int? ClasificadoId { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
        public bool Finalizado { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime Fecha { get; set; }
        public string? Grupo { get; set; }
    }
}
