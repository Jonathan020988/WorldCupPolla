namespace WorldCup.App.Shared.DTOs
{
    public class CrearInvitacionPollaDto
    {
        public int RemitenteId { get; set; }
        public string EmailInvitado { get; set; } = "";
        public string LinkInvitacion { get; set; } = "";
    }
}
