using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs
{
    public class SolicitudIngresoDto
    {
        public int Id { get; set; }
        public int PollaId { get; set; }
        public string PollaNombre { get; set; } = "";
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public string Estado { get; set; } = "";
        public DateTime FechaSolicitud { get; set; }
    }
}
