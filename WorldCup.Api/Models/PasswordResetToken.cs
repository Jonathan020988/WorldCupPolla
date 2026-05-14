using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorldCup.Api.Models
{
    [Table("PasswordResetTokens")]
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public string TokenHash { get; set; } = null!;

        [Required]
        public DateTime ExpiraEn { get; set; }

        public bool Usado { get; set; }

        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        public Usuario Usuario { get; set; } = null!;
    }
}
