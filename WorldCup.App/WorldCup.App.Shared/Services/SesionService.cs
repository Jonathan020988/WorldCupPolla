using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldCup.App.Shared.Services
{
    public class SesionService
    {
        public int? UsuarioId { get; private set; }
        public string? Nombre { get; private set; }

        public bool EstaLogueado => UsuarioId.HasValue;

        public void Login(int usuarioId, string nombre)
        {
            UsuarioId = usuarioId;
            Nombre = nombre;
        }

        public void Logout()
        {
            UsuarioId = null;
            Nombre = null;
        }
    }
}

