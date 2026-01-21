namespace WorldCup.Api.DTOs
{
    public class TablaPartidoDTO
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = string.Empty;

        public int? GolesLocalPred { get; set; }
        public int? GolesVisitantePred { get; set; }

        public int GolesLocalReal { get; set; }
        public int GolesVisitanteReal { get; set; }

        public int Puntos { get; set; }
    }
}
