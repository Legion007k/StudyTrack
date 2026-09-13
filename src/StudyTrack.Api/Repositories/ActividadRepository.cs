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

    public Task<Actividad?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        context.Actividades
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

    public async Task DeleteAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        // Se borra el subarbol explicitamente: la cascada de la base de datos no aplica a todos
        // los proveedores (InMemory) ni a entidades que no esten siendo rastreadas.
        context.Actividades.RemoveRange(ConDescendientes(actividad));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Actividad> ConDescendientes(Actividad actividad) =>
        actividad.ActividadesHijas.SelectMany(ConDescendientes).Append(actividad);
}
