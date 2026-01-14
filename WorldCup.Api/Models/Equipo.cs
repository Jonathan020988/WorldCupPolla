namespace WorldCup.Api.Models
{
    public class Equipo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string CodigoFifa { get; set; } = null!;

        // 👇 SOLO STRING, NADA DE RELACIÓN
        public string Grupo { get; set; } = null!;

        public string BanderaUrl { get; set; } = null!;

        public List<Partido>? PartidosLocal { get; set; }
        public List<Partido>? PartidosVisitante { get; set; }
    }

}