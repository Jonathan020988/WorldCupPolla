namespace WorldCup.Api.DTOs
{
    public class PollaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int CreadorId { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int? MaximoMiembros { get; set; }
        public bool PermitirEmpatesEnEliminatoria { get; set; }
    }


}
