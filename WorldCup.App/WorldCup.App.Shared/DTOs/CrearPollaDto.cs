using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs
{
    public class CrearPollaDto
    {
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public int CreadorId { get; set; }
        public int? MaximoMiembros { get; set; }
        public bool PermitirEmpatesEnEliminatoria { get; set; }

        // 🔐 PIN de 4 dígitos
        public string PinIngreso { get; set; } = null!;
    }
}
