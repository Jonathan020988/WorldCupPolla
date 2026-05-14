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
        public int? PollaActivaId { get; private set; }
        public string? PollaActivaNombre { get; private set; }
        public bool EsAdmin { get; private set; }

        public bool EstaLogueado => UsuarioId.HasValue;

        // 🔔 Evento para notificar cambios
        public event Action? OnChange;

        public SesionService(LocalStorageService storage)
        {
            _storage = storage;
        }

        public async Task LoginAsync(int usuarioId, string nombre, bool esAdmin = false)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;
            EsAdmin = esAdmin;
            PollaActivaId = null;
            PollaActivaNombre = null;

            await GuardarSesionAsync();
            NotifyStateChanged();
        }

        public async Task RestaurarSesionAsync()
        {
            var data = await _storage.GetAsync<SesionDto>(KEY);
            if (data != null)
            {
                UsuarioId = data.UsuarioId;
                Nombre = data.Nombre;
                EsAdmin = data.EsAdmin;
                PollaActivaId = data.PollaActivaId;
                PollaActivaNombre = data.PollaActivaNombre;
                NotifyStateChanged();
            }
        }

        public async Task SeleccionarPollaAsync(int pollaId, string nombre)
        {
            PollaActivaId = pollaId;
            PollaActivaNombre = nombre;

            await GuardarSesionAsync();
            NotifyStateChanged();
        }

        public async Task LimpiarPollaActivaAsync()
        {
            PollaActivaId = null;
            PollaActivaNombre = null;

            await GuardarSesionAsync();
            NotifyStateChanged();
        }

        public void Login(int usuarioId, string nombre)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;
            PollaActivaId = null;
            PollaActivaNombre = null;
            NotifyStateChanged();
        }

        public async Task LogoutAsync()
        {
            UsuarioId = null;
            Nombre = null;
            EsAdmin = false;
            PollaActivaId = null;
            PollaActivaNombre = null;
            await _storage.RemoveAsync(KEY);
            NotifyStateChanged();
        }


        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }

        private async Task GuardarSesionAsync()
        {
            await _storage.SetAsync(KEY, new
            {
                UsuarioId,
                Nombre,
                EsAdmin,
                PollaActivaId,
                PollaActivaNombre
            });
        }

        public class SesionDto
        {
            public int UsuarioId { get; set; }
            public string? Nombre { get; set; }
            public bool EsAdmin { get; set; }
            public int? PollaActivaId { get; set; }
            public string? PollaActivaNombre { get; set; }
        }
    }
}

