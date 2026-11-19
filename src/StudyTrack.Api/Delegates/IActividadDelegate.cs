using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Delegates;

/// <summary>
/// Todas las consultas y cambios reciben el usuario que los solicita (<c>callerUserId</c>):
/// no hay operaciones anonimas. En CreateAsync el solicitante es el dueno (actividad.UserId).
/// </summary>
public interface IActividadDelegate
{
    /// <summary>Actividades visibles para el usuario, como arbol: raices con su subarbol anidado.</summary>
    Task<IReadOnlyList<Actividad>> GetAllAsync(string callerUserId, CancellationToken cancellationToken = default);

    /// <summary>Actividades visibles para el usuario que se traslapan con [desde, hasta], como arbol.</summary>
    Task<IReadOnlyList<Actividad>> GetCalendarioAsync(
        DateTime desde, DateTime hasta, string callerUserId, CancellationToken cancellationToken = default);

    /// <summary>La actividad con su subarbol anidado, o null si no existe. Lanza ForbiddenException si no tiene acceso.</summary>
    Task<Actividad?> GetByIdAsync(string id, string callerUserId, CancellationToken cancellationToken = default);

    /// <summary>Crea la actividad y devuelve el ID generado por el servidor.</summary>
    Task<string> CreateAsync(Actividad actividad, CancellationToken cancellationToken = default);

    /// <summary>Actualiza los campos editables. Dueno y colaboradores pueden editar; solo el dueno cambia colaboradores.</summary>
    Task<Actividad> UpdateAsync(string id, Actividad actividad, string callerUserId, CancellationToken cancellationToken = default);

    /// <summary>Comparte la actividad con otro usuario registrado. Solo el dueno puede hacerlo.</summary>
    Task CompartirAsync(string id, string usuarioIdDestino, string callerUserId, CancellationToken cancellationToken = default);

    /// <summary>Elimina logicamente la actividad y su subarbol. Solo el dueno puede hacerlo.</summary>
    Task DeleteAsync(string id, string callerUserId, CancellationToken cancellationToken = default);
}
