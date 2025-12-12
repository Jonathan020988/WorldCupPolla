using Microsoft.AspNetCore.Identity;

namespace WorldCup.Api.Models
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; }
    }
}
