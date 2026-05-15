namespace WorldCup.Api.Models
{
    public class EmailVerificationToken
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;
        public string TokenHash { get; set; } = "";
        public DateTime ExpiraEn { get; set; }
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public bool Usado { get; set; }
    }
}
