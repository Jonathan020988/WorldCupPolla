namespace WorldCup.Api.Models
{
    public class PrediccionTercero
    {
        public int Id { get; set; }
        public int PollaId { get; set; }
        public int UsuarioId { get; set; }
        public string Grupo { get; set; } = string.Empty;
    }
}
