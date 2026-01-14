namespace WorldCup.Api.DTOs
{
    public class TeamDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Abreviatura { get; set; } = null!;
        public string BanderaUrl { get; set; } = null!;
        public string Grupo { get; set; } = null!;

    }


}
