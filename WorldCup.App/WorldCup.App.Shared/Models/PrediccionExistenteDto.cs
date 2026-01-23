using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.Models
{
    public class PrediccionExistenteDto
    {
        public int PartidoId { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public int PuntosMarcador { get; set; }
        public int PuntosTotales { get; set; }
    }

}
