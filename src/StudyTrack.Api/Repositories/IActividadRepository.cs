using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public interface IActividadRepository
{
    /// <summary>
    /// Lista plana de las actividades que el usuario puede ver: propias, compartidas con el
    /// y las que cuelgan de una actividad compartida con el.
    /// </summary>
    Task<IReadOnlyList<Actividad>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Igual que <see cref="GetAllAsync"/>, limitado a las que se traslapan con [desde, hasta].</summary>
    Task<IReadOnlyList<Actividad>> GetPorRangoFechasAsync(
        DateTime desde, DateTime hasta, string userId, CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol de hijas cargado, o null si no existe.</summary>
    Task<Actividad?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Indica si otra actividad del usuario ya tiene el mismo titulo y fecha de fin.</summary>
    Task<bool> ExistsDuplicateAsync(
        string userId, string titulo, DateTime fechaFin, string? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(Actividad actividad, CancellationToken cancellationToken = default);

    Task UpdateAsync(Actividad actividad, CancellationToken cancellationToken = default);

    /// <summary>Verifica si existe un usuario registrado con el ID especificado.</summary>
    Task<bool> ExisteUsuarioAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Registra un colaborador para compartir la actividad.</summary>
    Task CompartirAsync(Actividad actividad, string usuarioId, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>Elimina logicamente la actividad y todas sus descendientes con la fecha indicada.</summary>
    Task DeleteAsync(Actividad actividad, DateTime ahora, CancellationToken cancellationToken = default);
}
