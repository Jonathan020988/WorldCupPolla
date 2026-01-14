namespace WorldCup.Api.Models
{
    public class PrediccionGrupo
    {
        public int Id { get; set; }

        public int PollaId { get; set; }
        public int UsuarioId { get; set; }

        public string Grupo { get; set; } = null!;

        public int PrimeroId { get; set; }
        public int SegundoId { get; set; }
        public int TerceroId { get; set; }

        public bool Bloqueada { get; set; }
    }

}
