using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace WorldCup.App.Shared.DTOs
{
    public class SesionDto
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public int? PollaActivaId { get; set; }
        public string? PollaActivaNombre { get; set; }
    }
}
