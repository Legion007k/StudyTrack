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

    public async Task<IReadOnlyList<Actividad>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Actividades
            .AsNoTracking()
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Actividad>> GetPorRangoFechasAsync(
        DateTime desde, DateTime hasta, CancellationToken cancellationToken = default) =>
        await context.Actividades
            .AsNoTracking()
            .Where(a => a.FechaInicio <= hasta && a.FechaFin >= desde)
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

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

    public async Task CompartirAsync(Actividad actividad, string usuarioId, DateTime ahora, CancellationToken cancellationToken = default)
    {
        if (context.Entry(actividad).State == EntityState.Detached)
            context.Actividades.Attach(actividad);

        actividad.CompartirCon(usuarioId, ahora);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        // Borrado logico (soft delete): se marca la actividad y todo su subarbol como eliminadas
        // conservando los datos fisicos en la base de datos.
        var ahora = DateTime.Now;
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
