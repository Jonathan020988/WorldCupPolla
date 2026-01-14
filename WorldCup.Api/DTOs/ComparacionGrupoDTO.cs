namespace WorldCup.Api.DTOs
{
    public class ComparacionGrupoDTO
    {
        public int EquipoId { get; set; }
        public string Equipo { get; set; } = null!;
        public int PosicionReal { get; set; }
        public int PosicionPredicha { get; set; }
        public bool PosicionExacta { get; set; }
        public bool ClasificadoCorrecto { get; set; }
    }
}
