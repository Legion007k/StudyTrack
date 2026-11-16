using Microsoft.EntityFrameworkCore;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Actividad> Actividades => Set<Actividad>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ActividadCompartida> ActividadesCompartidas => Set<ActividadCompartida>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.ToTable("Usuarios");
            usuario.HasKey(u => u.Id);
            usuario.Property(u => u.Id).HasMaxLength(64);
            usuario.Property(u => u.Nombre).HasMaxLength(200).IsRequired();
            usuario.Property(u => u.Email).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Actividad>(actividad =>
        {
            actividad.ToTable("Actividades");
            actividad.HasKey(a => a.Id);

            actividad.Property(a => a.Id).HasMaxLength(64);
            actividad.Property(a => a.UserId).HasMaxLength(64).IsRequired();
            actividad.Property(a => a.Titulo).HasMaxLength(200).IsRequired();
            actividad.Property(a => a.Descripcion).HasMaxLength(2000);
            actividad.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
            actividad.Property(a => a.ActividadPadreId).HasMaxLength(64);

            // Filtro global: consultas normales ignoran actividades eliminadas logicamente.
            actividad.HasQueryFilter(a => !a.Eliminada);

            // Respaldo en base de datos de la regla de duplicados que valida el delegate.
            // Solo aplica a actividades no eliminadas para permitir recrear actividades con mismo titulo.
            actividad.HasIndex(a => new { a.UserId, a.Titulo, a.FechaFin })
                .IsUnique()
                .HasFilter("\"Eliminada\" = false");

            // Borrar una actividad se lleva todo su subarbol.
            actividad.HasOne(a => a.ActividadPadre)
                .WithMany(a => a.ActividadesHijas)
                .HasForeignKey(a => a.ActividadPadreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActividadCompartida>(compartida =>
        {
            compartida.ToTable("ActividadesCompartidas");
            compartida.HasKey(ac => new { ac.ActividadId, ac.UsuarioId });

            compartida.Property(ac => ac.ActividadId).HasMaxLength(64);
            compartida.Property(ac => ac.UsuarioId).HasMaxLength(64);

            compartida.HasQueryFilter(ac => !ac.Actividad!.Eliminada);

            compartida.HasOne(ac => ac.Actividad)
                .WithMany(a => a.Colaboradores)
                .HasForeignKey(ac => ac.ActividadId)
                .OnDelete(DeleteBehavior.Cascade);

            compartida.HasOne(ac => ac.Usuario)
                .WithMany()
                .HasForeignKey(ac => ac.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de DateTime a UTC:
        // Todas las propiedades DateTime y DateTime? se persisten como "timestamp with time zone" (timestamptz).
        // Se configuran ValueConverters para asegurar que tanto al escribir como al leer de PostgreSQL,
        // el DateTimeKind sea estrictamente Utc, evitando desfases de 7 horas por zonas horarias del cliente o servidor.
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var utcNullableConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        var propiedadesDeFecha = modelBuilder.Model.GetEntityTypes()
            .SelectMany(entidad => entidad.GetProperties())
            .Where(propiedad => propiedad.ClrType == typeof(DateTime) || propiedad.ClrType == typeof(DateTime?));

        foreach (var propiedad in propiedadesDeFecha)
        {
            if (propiedad.ClrType == typeof(DateTime))
                propiedad.SetValueConverter(utcConverter);
            else
                propiedad.SetValueConverter(utcNullableConverter);

            propiedad.SetColumnType("timestamp with time zone");
        }
    }
}
