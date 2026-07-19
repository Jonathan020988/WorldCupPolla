using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs
{
   

    public class RankingPollaDto
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public string ObservacionAdmin { get; set; } = "";
        public int Puntos { get; set; }
        public int? PosicionAnterior { get; set; }
        public int? CambioPosicion { get; set; }
        public decimal? Premio { get; set; }
        public bool TienePodio { get; set; }
        public string PodioCampeon { get; set; } = "";
        public string PodioSubcampeon { get; set; } = "";
        public string PodioTercero { get; set; } = "";
    }

}
