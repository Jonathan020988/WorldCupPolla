using System;

namespace WorldCup.Api.Models
{
    public class Team
    {
        public int Id { get; set; }                // PK (int auto increment)
        public string Name { get; set; } = null!; // Nombre completo, ej. "Argentina"
        public string ShortName { get; set; } = null!; // Abreviatura, ej. "ARG"
        public string? FlagUrl { get; set; }       // URL a imagen (opcional)
        public string? Group { get; set; }         // Grupo A, B, etc (opcional)
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
