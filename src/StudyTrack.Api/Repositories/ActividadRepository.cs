using Microsoft.EntityFrameworkCore;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public class ActividadRepository(ApplicationDbContext context) : IActividadRepository
{
    /// <summary>
    /// "ActividadesHijas.ActividadesHijas.ActividadesHijas": ningun arbol pasa de la profundidad
    /// maxima absoluta, asi que con este Include el subarbol de cualquier nodo llega completo.
    /// </summary>
    private static readonly string IncludeSubarbol = string.Join(
        ".", Enumerable.Repeat(nameof(Actividad.ActividadesHijas), Actividad.ProfundidadMaximaAbsoluta));

    public async Task<IReadOnlyList<Actividad>> GetAllAsync(string userId, CancellationToken cancellationToken = default) =>
        await AccesiblesPor(context.Actividades.AsNoTracking().Include(a => a.Colaboradores), userId)
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Actividad>> GetPorRangoFechasAsync(
        DateTime desde, DateTime hasta, string userId, CancellationToken cancellationToken = default) =>
        await AccesiblesPor(context.Actividades.AsNoTracking().Include(a => a.Colaboradores), userId)
            .Where(a => a.FechaInicio <= hasta && a.FechaFin >= desde)
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Actividades que el usuario puede ver: las suyas, las compartidas con el y las que cuelgan
    /// (a cualquier nivel) de una actividad compartida con el. La profundidad maxima es 3, asi que
    /// basta con revisar hasta el bisabuelo.
    /// </summary>
    private static IQueryable<Actividad> AccesiblesPor(IQueryable<Actividad> query, string userId) =>
        query.Where(a =>
            a.UserId == userId
            || a.Colaboradores!.Any(c => c.UsuarioId == userId)
            || (a.ActividadPadre != null
                && (a.ActividadPadre.UserId == userId
                    || a.ActividadPadre.Colaboradores!.Any(c => c.UsuarioId == userId)
                    || (a.ActividadPadre.ActividadPadre != null
                        && (a.ActividadPadre.ActividadPadre.UserId == userId
                            || a.ActividadPadre.ActividadPadre.Colaboradores!.Any(c => c.UsuarioId == userId)
                            || (a.ActividadPadre.ActividadPadre.ActividadPadre != null
                                && (a.ActividadPadre.ActividadPadre.ActividadPadre.UserId == userId
                                    || a.ActividadPadre.ActividadPadre.ActividadPadre.Colaboradores!.Any(c => c.UsuarioId == userId))))))));

    public Task<Actividad?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        context.Actividades
            .Include(a => a.Colaboradores)
            .Include(IncludeSubarbol)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsDuplicateAsync(
        string userId, string titulo, DateTime fechaFin, string? excludeId, CancellationToken cancellationToken = default) =>
        context.Actividades.AnyAsync(
            a => a.UserId == userId
                 && a.Titulo == titulo
                 && a.FechaFin == fechaFin
                 && (excludeId == null || a.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        context.Actividades.Add(actividad);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        if (context.Entry(actividad).State == EntityState.Detached)
            context.Actividades.Update(actividad);

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExisteUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default) =>
        context.Usuarios.AnyAsync(u => u.Id == usuarioId, cancellationToken);

    public async Task CompartirAsync(Actividad actividad, string usuarioId, DateTime ahora, CancellationToken cancellationToken = default)
    {
        if (context.Entry(actividad).State == EntityState.Detached)
            context.Actividades.Attach(actividad);

        actividad.CompartirCon(usuarioId, ahora);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Actividad actividad, DateTime ahora, CancellationToken cancellationToken = default)
    {
        // Borrado logico (soft delete): se marca la actividad y todo su subarbol como eliminadas
        // conservando los datos fisicos en la base de datos. La hora la decide el delegate (TimeProvider).
        foreach (var nodo in ConDescendientes(actividad))
        {
            nodo.MarcarEliminada(ahora);
            if (context.Entry(nodo).State == EntityState.Detached)
                context.Actividades.Update(nodo);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Actividad> ConDescendientes(Actividad actividad) =>
        actividad.ActividadesHijas.SelectMany(ConDescendientes).Append(actividad);
}
