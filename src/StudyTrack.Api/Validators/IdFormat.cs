using System.Text.RegularExpressions;

namespace StudyTrack.Api.Validators;

/// <summary>Formato de ID del contrato: [A-Za-z0-9\-]+.</summary>
public static partial class IdFormat
{
    public const string Mensaje = "El ID solo puede contener letras, numeros y guiones.";

    // \A y \z en vez de ^ y $: "$" aceptaria un salto de linea final ("abc%0A").
    [GeneratedRegex(@"\A[A-Za-z0-9\-]+\z")]
    private static partial Regex Patron();

    public static bool EsValido(string? id) => id is not null && Patron().IsMatch(id);
}
