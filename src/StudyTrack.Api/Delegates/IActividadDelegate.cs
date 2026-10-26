using StudyTrack.Api.Dtos;

namespace StudyTrack.Api.Delegates;

public interface IActividadDelegate
{
    /// <summary>Actividades raiz, cada una con su subarbol anidado.</summary>
    Task<IReadOnlyList<ActividadDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Actividades en el rango de fechas para vista de calendario.</summary>
    Task<IReadOnlyList<ActividadDto>> GetCalendarioAsync(
        DateTime desde, DateTime hasta, CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol anidado, o null si no existe.</summary>
    Task<ActividadDto?> GetByIdAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Crea la actividad y devuelve el ID generado.</summary>
    Task<string> CreateAsync(CreateActividadDto dto, CancellationToken cancellationToken = default);

    Task<ActividadDto> UpdateAsync(string id, UpdateActividadDto dto, string? callerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Comparte la actividad con otro usuario.</summary>
    Task CompartirAsync(string id, string usuarioIdDestino, string? callerUserId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default);
}
