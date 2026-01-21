using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorldCup.Api.Models
{
    public class Prediccion
    {
        public int Id { get; set; }

        [Required]
        public int PollaId { get; set; }
        public Polla Polla { get; set; } = null!;

        

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;


        [Required]
        public int PartidoId { get; set; }
        public Partido Partido { get; set; } = null!;

        // Marcador
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }

        // Eliminatorias
        public bool PrediceTiempoExtra { get; set; }
        public bool PredicePenales { get; set; }

        // Equipo que clasifica (KO)
        public int? PrediceClasificadoId { get; set; }

        // Puntos (se calculan después)
        public int PuntosTotales { get; set; }
        public int PuntosMarcador { get; set; }
        public int PuntosClasificacion { get; set; }
        public int PuntosPodio { get; set; }

        public string? Grupo { get; set; }
        public int? PrediceSegundoId { get; set; }

        // Control
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public bool Bloqueada { get; set; }

    }
}

