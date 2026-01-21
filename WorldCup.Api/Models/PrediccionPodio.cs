using System.ComponentModel.DataAnnotations;

namespace WorldCup.Api.Models
{
    public class PrediccionPodio
    {
        public int Id { get; set; }

        [Required]
        public int PollaId { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public int CampeonId { get; set; }

        [Required]
        public int SubcampeonId { get; set; }

        [Required]
        public int TerceroId { get; set; }

        public bool Bloqueada { get; set; } = false;
    }
}
