namespace WorldCup.Api.DTOs
{
    public class ResumenFinalPollaDTO
    {
        public int PollaId { get; set; }
        public List<ResumenGrupoDTO> Grupos { get; set; } = new();
    }
}
