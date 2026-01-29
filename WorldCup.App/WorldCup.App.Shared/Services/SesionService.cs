using WorldCup.App.Shared.DTOs;
using WorldCup.App.Shared.Services;


namespace WorldCup.App.Shared.Services
{
    public class SesionService

    {
        private readonly LocalStorageService _storage;
        private const string KEY = "sesion";

        public int? UsuarioId { get; private set; }
        public string? Nombre { get; private set; }

        public bool EstaLogueado => UsuarioId.HasValue;

        // 🔔 Evento para notificar cambios
        public event Action? OnChange;

        public SesionService(LocalStorageService storage)
        {
            _storage = storage;
        }

        public async Task LoginAsync(int usuarioId, string nombre)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;

            await _storage.SetAsync(KEY, new { UsuarioId, Nombre });
            NotifyStateChanged();
        }

        public async Task RestaurarSesionAsync()
        {
            var data = await _storage.GetAsync<SesionDto>(KEY);
            if (data != null)
            {
                UsuarioId = data.UsuarioId;
                Nombre = data.Nombre;
                NotifyStateChanged();
            }
        }
        public void Login(int usuarioId, string nombre)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;
            NotifyStateChanged();
        }

        public async Task LogoutAsync()
        {
            UsuarioId = null;
            Nombre = null;
            await _storage.RemoveAsync(KEY);
            NotifyStateChanged();
        }


        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }

        public class SesionDto
        {
            public int UsuarioId { get; set; }
            public string? Nombre { get; set; }
        }
    }
}

