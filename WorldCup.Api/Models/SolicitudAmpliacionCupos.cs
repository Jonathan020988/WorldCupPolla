using System.ComponentModel.DataAnnotations.Schema;

namespace WorldCup.Api.Models
{
    [Table("SolicitudesAmpliacionCupos")]
    public class SolicitudAmpliacionCupos
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public string Celular { get; set; } = "";
        public int CantidadUsuariosSolicitada { get; set; }
        public string PlanNombre { get; set; } = "";
        public decimal ValorPlan { get; set; }

        public string Estado { get; set; } = "Pendiente";
        public string? CodigoHabilitacion { get; set; }
        public int? MaximoMiembrosAutorizado { get; set; }

        public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
        public DateTime? FechaCodigo { get; set; }
        public DateTime? FechaActivacion { get; set; }

        public int? AdminUsuarioId { get; set; }
        public Usuario? AdminUsuario { get; set; }
    }
}
