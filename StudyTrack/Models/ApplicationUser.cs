using Microsoft.AspNetCore.Identity;

namespace StudyTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Nombre { get; set; }
        public string Tema { get; set; } = "light";
        // cualquier otra personalización futura
    }


}
