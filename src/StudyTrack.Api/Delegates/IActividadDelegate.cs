using StudyTrack.Api.Dtos;

namespace StudyTrack.Api.Delegates;

public interface IActividadDelegate
{
    /// <summary>Actividades raiz, cada una con su subarbol anidado.</summary>
    Task<IReadOnlyList<ActividadDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol anidado, o null si no existe.</summary>
    Task<ActividadDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Crea la actividad y devuelve el ID generado.</summary>
    Task<string> CreateAsync(CreateActividadDto dto, CancellationToken cancellationToken = default);

    Task<ActividadDto> UpdateAsync(string id, UpdateActividadDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default); 
    //tener un soft delete para que no se pierdan los datos de las actividades y puedan ser recuperadas en caso de error.
}
