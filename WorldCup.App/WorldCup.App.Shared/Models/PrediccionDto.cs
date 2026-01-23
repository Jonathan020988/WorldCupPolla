using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.Models
{
    public class PrediccionDto
    {
        public int Id { get; set; }

        // Relaciones
        public int PartidoId { get; set; }
        public string UsuarioId { get; set; } = string.Empty;

        // Predicción
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }

        // Resultado calculado
        public int Puntos { get; set; }

        public DateTime FechaPrediccion { get; set; }

        // Estado
        public bool PartidoFinalizado { get; set; }
    }

}
