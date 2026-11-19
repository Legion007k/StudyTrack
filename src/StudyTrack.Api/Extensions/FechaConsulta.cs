using System.Globalization;

namespace StudyTrack.Api.Extensions;

/// <summary>Lee fechas de la query string con la misma regla que el cuerpo: deben traer zona horaria.</summary>
public static class FechaConsulta
{
    public const string MensajeSinZona =
        "La fecha debe incluir zona horaria (Z u offset), por ejemplo 2026-09-20T00:00:00Z o 2026-09-20T00:00:00-07:00.";

    /// <summary>
    /// Devuelve null y el instante en UTC si la fecha es valida; si no, el mensaje de error.
    /// RoundtripKind deja Kind=Unspecified solo cuando el texto no traia "Z" ni offset.
    /// </summary>
    public static string? TryParseUtc(string? valor, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrWhiteSpace(valor))
            return "Es obligatorio.";

        if (!DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            return "No es una fecha valida.";

        if (fecha.Kind == DateTimeKind.Unspecified)
            return MensajeSinZona;

        utc = fecha.ToUniversalTime();
        return null;
    }
}
