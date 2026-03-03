using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs
{
    public class UnirsePollaDTO
    {
        public int UsuarioId { get; set; }
        public int PollaId { get; set; }
        public string PinIngreso { get; set; } = "";
    }
}
