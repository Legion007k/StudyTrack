namespace StudyTrack.Api.Domain;

/// <summary>
/// Cuerpo de POST /actividades/{id}/compartir. Quien comparte viaja en el cuerpo (userId), igual que
/// en el resto de las operaciones que modifican datos: los headers quedan solo para metadatos.
/// </summary>
public record CompartirActividadRequest
{
    /// <summary>Usuario que solicita compartir. Debe ser el dueno de la actividad.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Usuario destinatario con quien se comparte la actividad. Debe estar registrado.</summary>
    public string UsuarioId { get; init; } = string.Empty;
}
