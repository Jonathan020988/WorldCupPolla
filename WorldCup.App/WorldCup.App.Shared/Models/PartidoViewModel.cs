using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.Models
{
    public class PartidoViewModel
    {
        public int Id { get; set; }
        public string Fase { get; set; } = string.Empty;
        public string Local { get; set; } = string.Empty;
        public string Visitante { get; set; } = string.Empty;
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool Finalizado { get; set; }
    }
}
