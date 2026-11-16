namespace StudyTrack.Api.Domain;

/// <summary>
/// Modelo de solicitud para eliminar una actividad.
/// El usuario que solicita la eliminación viaja en el cuerpo (Body) de la petición,
/// cumpliendo con el principio REST de que los headers son únicamente metadatos.
/// </summary>
public record DeleteActividadRequest
{
    /// <summary>Identificador del usuario que solicita eliminar la actividad.</summary>
    public string? UserId { get; init; }
}

