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

        // Relaciones
        public List<Polla> PollasCreadas { get; set; } = new();
        public List<PollaMiembro> PollaMiembros { get; set; } = new();
    }
}
