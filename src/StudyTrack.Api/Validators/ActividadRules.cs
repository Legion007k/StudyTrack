using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

/// <summary>Reglas de formato compartidas por los validadores de la API.</summary>
internal static class ActividadRules
{
    public const string MensajeFechaSinZona =
        "La fecha debe incluir zona horaria (Z u offset), por ejemplo 2026-09-20T10:00:00Z o 2026-09-20T10:00:00-07:00.";

    public static void TituloValido<T>(this IRuleBuilder<T, string> regla) =>
        regla.NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede superar 200 caracteres.");

    public static void DescripcionValida<T>(this IRuleBuilder<T, string?> regla) =>
        regla.MaximumLength(2000).WithMessage("La descripcion no puede superar 2000 caracteres.");

    /// <summary>
    /// Las fechas son instantes absolutos: deben traer "Z" u offset. Una fecha sin zona es ambigua
    /// (no se sabe de que zona es la hora de pared) y se rechaza en vez de adivinarla.
    /// </summary>
    public static void FechaFinValida<T>(this IRuleBuilder<T, DateTime> regla) =>
        regla.NotEmpty().WithMessage("La fecha de fin es obligatoria.")
            .Must(fecha => fecha == default || TieneZonaHoraria(fecha)).WithMessage(MensajeFechaSinZona);

    public static void TipoValido<T>(this IRuleBuilder<T, TipoActividad> regla) =>
        regla.IsInEnum().WithMessage("El tipo de actividad no es valido.");

    public static void ActividadPadreIdValido<T>(this IRuleBuilder<T, string?> regla) =>
        regla.Must(id => id is null || IdFormat.EsValido(id)).WithMessage(IdFormat.Mensaje);

    /// <summary>Identificador de usuario obligatorio: hasta 64 caracteres con el formato de ID.</summary>
    public static void UsuarioIdValido<T>(this IRuleBuilderInitial<T, string> regla, string campo) =>
        regla.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage($"El {campo} es obligatorio.")
            .MaximumLength(64).WithMessage($"El {campo} no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje);

    /// <summary>System.Text.Json deja Kind=Unspecified solo cuando el texto no traia zona horaria.</summary>
    public static bool TieneZonaHoraria(DateTime fecha) => fecha.Kind != DateTimeKind.Unspecified;
}
