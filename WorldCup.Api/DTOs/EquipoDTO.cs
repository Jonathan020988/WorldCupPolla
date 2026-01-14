namespace WorldCup.Api.DTOs
{
    public class EquipoDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string CodigoFifa { get; set; } = null!;
        public string BanderaUrl { get; set; } = null!;

       
    }
}
