namespace WorldCup.Api.DTOs
{
    public class ActualizarPremiosPollaDTO
    {
        public int SolicitanteId { get; set; }
        public decimal? PremioPrimerLugar { get; set; }
        public decimal? PremioSegundoLugar { get; set; }
        public decimal? PremioTercerLugar { get; set; }
    }
}
