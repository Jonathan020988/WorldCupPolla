namespace WorldCup.App.Shared.DTOs
{
    public class ClasificacionUsuarioDetalleDto
    {
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public List<GrupoClasificacionUsuarioDto> Clasificacion { get; set; } = new();
        public List<string> MejoresTerceros { get; set; } = new();
    }

    public class GrupoClasificacionUsuarioDto
    {
        public string Grupo { get; set; } = "";
        public int PrimeroId { get; set; }
        public string Primero { get; set; } = "";
        public int SegundoId { get; set; }
        public string Segundo { get; set; } = "";
        public int TerceroId { get; set; }
        public string Tercero { get; set; } = "";
        public bool Bloqueada { get; set; }
    }
}
