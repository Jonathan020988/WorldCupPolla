namespace WorldCup.App.Shared.DTOs
{
    public class EquipoGrupoDto
    {
        public int EquipoId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Grupo { get; set; } = string.Empty;

        public int Puntos { get; set; }

        public int GolesFavor { get; set; }

        public int GolesContra { get; set; }

        public int Diferencia =>
            GolesFavor - GolesContra;
    }
}
