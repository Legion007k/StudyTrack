using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Delegates;

public interface IActividadDelegate
{
    /// <summary>Actividades raiz, cada una con su subarbol anidado, opcionalmente filtradas por usuario.</summary>
    Task<IReadOnlyList<Actividad>> GetAllAsync(string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Actividades en el rango de fechas para vista de calendario, opcionalmente filtradas por usuario.</summary>
    Task<IReadOnlyList<Actividad>> GetCalendarioAsync(
        DateTime desde, DateTime hasta, string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol anidado, o null si no existe.</summary>
    Task<Actividad?> GetByIdAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Crea la actividad y devuelve el ID generado.</summary>
    Task<string> CreateAsync(Actividad actividad, CancellationToken cancellationToken = default);

    Task<Actividad> UpdateAsync(string id, Actividad actividad, string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Comparte la actividad con otro usuario.</summary>
    Task CompartirAsync(string id, string usuarioIdDestino, string? callerUserId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default);
}
