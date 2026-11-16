namespace StudyTrack.Api.Delegates.Exceptions;

/// <summary>
/// Representa un error de validación o solicitud inválida que debe responder con HTTP 400 Bad Request.
/// Se utiliza para rechazar operaciones antes de interactuar con la persistencia
/// (por ejemplo, si el usuario destinatario no existe al compartir una actividad).
/// </summary>
public class BadRequestException : Exception
{
    public string Campo { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public BadRequestException(string campo, string mensaje) : base(mensaje)
    {
        Campo = campo;
        Errors = new Dictionary<string, string[]> { [campo] = [mensaje] };
    }

    public BadRequestException(string mensaje) : base(mensaje)
    {
        Campo = "solicitud";
        Errors = new Dictionary<string, string[]> { ["solicitud"] = [mensaje] };
    }
}

