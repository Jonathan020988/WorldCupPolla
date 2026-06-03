using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.DTOs
{
    public class PollaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadParticipantes { get; set; }
        public bool Activa { get; set; }
        public DateTime FechaCreacion { get; set; }   // 👈 ESTA FALTABA

        public string? Descripcion { get; set; }
        public int CreadorId { get; set; }
        public int? MaximoMiembros { get; set; }
        public bool InscripcionesAbiertas { get; set; } = true;
        public bool PermitirEmpatesEnEliminatoria { get; set; }
        public decimal? ValorInscripcion { get; set; }
        public string? MetodoPago { get; set; }
        public bool CuposIlimitados { get; set; }

        // 🔑 PIN
        public string? PinIngreso { get; set; }
    }
}
