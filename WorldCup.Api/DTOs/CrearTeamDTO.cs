namespace WorldCup.Api.DTOs
{
    public class CrearTeamDTO
    {
        public string Nombre { get; set; } = null!;
        public string Abreviatura { get; set; } = null!;
        public string BanderaUrl { get; set; } = null!;
        public string Grupo { get; set; } = null!;
    }
}
