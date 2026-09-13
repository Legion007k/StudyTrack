using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public interface IActividadRepository
{
    /// <summary>Todas las actividades, en una lista plana.</summary>
    Task<IReadOnlyList<Actividad>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol de hijas cargado, o null si no existe.</summary>
    Task<Actividad?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Indica si otra actividad del usuario ya tiene el mismo titulo y fecha de fin.</summary>
    Task<bool> ExistsDuplicateAsync(
        string userId, string titulo, DateTime fechaFin, string? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(Actividad actividad, CancellationToken cancellationToken = default);

    Task UpdateAsync(Actividad actividad, CancellationToken cancellationToken = default);

    /// <summary>Elimina la actividad y todas sus descendientes.</summary>
    Task DeleteAsync(Actividad actividad, CancellationToken cancellationToken = default);
}
