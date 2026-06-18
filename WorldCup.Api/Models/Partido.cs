namespace WorldCup.Api.Models
{
    public class Partido
    {
        public int Id { get; set; } // Clave primaria

        // Fecha del partido
        public DateTime Fecha { get; set; }

        // Fase del Mundial (Grupo, 16vos, 8vos, 4tos, Semi, Final, TercerPuesto)
        public string Fase { get; set; } = null!;

        // Equipos
        public int LocalId { get; set; } // FK al equipo local
        public Equipo Local { get; set; } = null!;

        public int VisitanteId { get; set; } // FK al equipo visitante
        public Equipo Visitante { get; set; } = null!;

        // Marcador oficial publicado
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }

        // Marcador provisional tomado de una fuente externa. No calcula puntos.
        public int? NumeroPartidoFifa { get; set; }
        public int? MarcadorEnVivoLocal { get; set; }
        public int? MarcadorEnVivoVisitante { get; set; }
        public string? EstadoMarcadorEnVivo { get; set; }
        public string? MinutoMarcadorEnVivo { get; set; }
        public DateTime? MarcadorEnVivoActualizadoEn { get; set; }
        public string? FuenteMarcadorEnVivo { get; set; }
        public string? IdExternoMarcadorEnVivo { get; set; }

        // ¿El partido ya terminó y tiene marcador oficial?
        public bool Finalizado { get; set; } = false;

        public string Estado { get; set; } = "Pendiente";

        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
        public bool TiempoExtra { get; set; } = false;
        public int? ClasificadoId { get; set; }

    }
}
