namespace WorldCup.Api.DTOs
{
    public class MiPosicionDTO
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public int Puntos { get; set; }
        public int Posicion { get; set; }
        public int TotalUsuarios { get; set; }
    }
}
