namespace StudyTrack.Api.Domain;

/// <summary>
/// Modelo de solicitud para compartir una actividad con otro usuario.
/// El usuario propietario (quien realiza la acción) ahora viaja en el cuerpo (Body)
/// como dato central de negocio, cumpliendo con la directriz de que los headers son solo metadatos.
/// </summary>
public record CompartirActividadRequest
{
    /// <summary>Usuario propietario que comparte la actividad (Body).</summary>
    public string? PropietarioId { get; init; }

    /// <summary>Alias alternativo para compatibilidad.</summary>
    public string? UserId { get; init; }

    /// <summary>Usuario destinatario con quien se comparte la actividad.</summary>
    public string UsuarioId { get; init; } = string.Empty;

    public string? ObtenerPropietario() =>
        !string.IsNullOrWhiteSpace(PropietarioId) ? PropietarioId : (!string.IsNullOrWhiteSpace(UserId) ? UserId : null);
}
