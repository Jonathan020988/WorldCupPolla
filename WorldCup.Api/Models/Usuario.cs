using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorldCup.Api.Models
{
    [Table("Usuarios")]
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = null!;

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public bool Activo { get; set; } = true;
        public bool EmailConfirmado { get; set; } = true;
        public DateTime? EmailConfirmadoEn { get; set; }
        public int MaximoMiembrosPorPolla { get; set; } = 5;
        public bool CuposIlimitados { get; set; } = false;
        public bool AceptaTerminos { get; set; } = false;
        public bool AceptaPoliticaPrivacidad { get; set; } = false;
        public bool AceptaTratamientoDatos { get; set; } = false;
        public string? VersionLegalAceptada { get; set; }
        public DateTime? LegalAceptadoEn { get; set; }
        public string? LegalAceptadoIp { get; set; }
        public string? LegalAceptadoUserAgent { get; set; }

        // Relaciones
        public List<Polla> PollasCreadas { get; set; } = new();
        public List<PollaMiembro> PollaMiembros { get; set; } = new();
        public List<SolicitudAmpliacionCupos> SolicitudesAmpliacionCupos { get; set; } = new();
        public List<AlertaUsuario> AlertasUsuario { get; set; } = new();
    }
}
