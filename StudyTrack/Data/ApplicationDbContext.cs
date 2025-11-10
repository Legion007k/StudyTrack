using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyTrack.Models;

namespace StudyTrack.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<Actividad> Actividades { get; set; }
        public DbSet<SesionEstudio> SesionesEstudio { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}
