namespace WorldCup.Api.DTOs
{
    public class PollaInvitacionDTO
    {
        public int Id { get; set; }
        public int PollaId { get; set; }
        public string EmailInvitado { get; set; } = null!;
        public bool Aceptada { get; set; }
    }

}
