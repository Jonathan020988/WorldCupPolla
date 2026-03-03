using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.Api.Models
{
    public class PollaSolicitud
    {
        public int Id { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string Estado { get; set; } = "Pendiente";
    }
}
