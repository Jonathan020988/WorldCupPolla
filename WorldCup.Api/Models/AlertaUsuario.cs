using System.ComponentModel.DataAnnotations.Schema;

namespace WorldCup.Api.Models
{
    [Table("AlertasUsuario")]
    public class AlertaUsuario
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public int? AdminUsuarioId { get; set; }
        public Usuario? AdminUsuario { get; set; }

        public int? PollaId { get; set; }
        public Polla? Polla { get; set; }

        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public string TipoDestino { get; set; } = "Predicciones";
        public string Link { get; set; } = "/predicciones";
        public string EtiquetaAccion { get; set; } = "Ir a predicciones";
        public string Estado { get; set; } = "Pendiente";
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaVista { get; set; }
        public DateTime? FechaCierre { get; set; }
    }
}
