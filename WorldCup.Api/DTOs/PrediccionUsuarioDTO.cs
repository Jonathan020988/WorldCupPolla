namespace WorldCup.Api.DTOs
{
    public class PrediccionUsuarioDTO
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public int Puntos { get; set; }
    }

}
