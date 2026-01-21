using System.ComponentModel.DataAnnotations;

namespace WorldCup.Api.DTOs
{
    public class GuardarPodioDTO
    {
        [Required]
        public int PollaId { get; set; }

        [Required]
        public int CampeonId { get; set; }

        [Required]
        public int SubcampeonId { get; set; }

        [Required]
        public int TerceroId { get; set; }
    }
}
