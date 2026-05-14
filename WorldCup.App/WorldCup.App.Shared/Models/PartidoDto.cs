using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.Models
{
    public class PartidoDto
    {
        public int Id { get; set; }
        public string Fase { get; set; }
        public string Local { get; set; }
        public string Visitante { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool Finalizado { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime Fecha { get; set; }
        public string? Grupo { get; set; }


    }
}
