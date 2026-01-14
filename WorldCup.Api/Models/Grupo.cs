namespace WorldCup.Api.Models
{
    public class Grupo
    {
        public int Id { get; set; } // Clave primaria

        public string Nombre { get; set; } = null!;
        // Ej: "A", "B", "C"... Nombre del grupo

        // Relación con equipos del grupo
        //public List<Equipo>? Equipos { get; set; }
    }
}
