namespace WorldCup.Api.DTOs
{
    public class RankingPollaDTO
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string ObservacionAdmin { get; set; } = string.Empty;
        public int Puntos { get; set; }
        public decimal? Premio { get; set; }
    }
}
