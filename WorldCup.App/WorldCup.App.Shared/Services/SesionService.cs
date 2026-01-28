namespace WorldCup.App.Shared.Services
{
    public class SesionService
    {
        public int? UsuarioId { get; private set; }
        public string? Nombre { get; private set; }

        public bool EstaLogueado => UsuarioId.HasValue;

        // 🔔 Evento para notificar cambios
        public event Action? OnChange;

        public void Login(int usuarioId, string nombre)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;
            NotifyStateChanged();
        }

        public void Logout()
        {
            UsuarioId = null;
            Nombre = null;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }
}

