using Microsoft.EntityFrameworkCore;
using StudyTrack.Models;

namespace StudyTrack.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Actividad> Actividades => Set<Actividad>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Actividad>(actividad =>
            {
                // Npgsql mapea DateTime a "timestamp with time zone" por defecto y rechaza
                // cualquier valor cuyo Kind no sea Utc. Las fechas de StudyTrack son horas de
                // pared del estudiante ("el examen es a las 10:00"), no instantes absolutos,
                // asi que se guardan sin zona horaria.
                actividad.Property(a => a.FechaInicio).HasColumnType("timestamp without time zone");
                actividad.Property(a => a.FechaFin).HasColumnType("timestamp without time zone");
                actividad.Property(a => a.FechaCompletada).HasColumnType("timestamp without time zone");

                // Borrar el evento ancla se lleva por delante todo lo que genero.
                actividad.HasOne(a => a.ActividadPadre)
                    .WithMany(a => a.ActividadesHijas)
                    .HasForeignKey(a => a.ActividadPadreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
