using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Repositories;

public interface IActividadRepository
{
    /// <summary>Todas las actividades, en una lista plana opcionalmente filtrada por usuario (duenio o colaborador).</summary>
    Task<IReadOnlyList<Actividad>> GetAllAsync(string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Actividades cuyo periodo de fechas se traslapa con el rango [desde, hasta], opcionalmente filtradas por usuario.</summary>
    Task<IReadOnlyList<Actividad>> GetPorRangoFechasAsync(
        DateTime desde, DateTime hasta, string? userId = null, CancellationToken cancellationToken = default);

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

    /// <summary>Elimina la actividad y todas sus descendientes.</summary>
    Task DeleteAsync(Actividad actividad, CancellationToken cancellationToken = default);
}
