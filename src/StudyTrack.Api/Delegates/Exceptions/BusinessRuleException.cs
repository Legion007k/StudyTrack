namespace StudyTrack.Api.Delegates.Exceptions;

/// <summary>La solicitud es valida en forma pero viola una regla de negocio.</summary>
public class BusinessRuleException(string campo, string message) : Exception(message)
{
    /// <summary>Errores por campo, con el nombre del campo tal como viaja en JSON.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } =
        new Dictionary<string, string[]> { [campo] = [message] };
}
