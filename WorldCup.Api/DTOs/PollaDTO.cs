namespace WorldCup.Api.DTOs
{
    public class PollaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int CreadorId { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CantidadParticipantes { get; set; }
        public int? MaximoMiembros { get; set; }
        public bool InscripcionesAbiertas { get; set; } = true;
        public bool PermitirEmpatesEnEliminatoria { get; set; }
        public decimal? ValorInscripcion { get; set; }
        public string? MetodoPago { get; set; }
        public bool CuposIlimitados { get; set; }

        public string? PinIngreso { get; set; }

    }


}
