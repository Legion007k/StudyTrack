using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

/// <summary>Reglas de formato compartidas por los DTOs de creacion y actualizacion.</summary>
internal static class ActividadRules
{
    public static void TituloValido<T>(this IRuleBuilder<T, string> regla) =>
        regla.NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede superar 200 caracteres.");

    public static void DescripcionValida<T>(this IRuleBuilder<T, string?> regla) =>
        regla.MaximumLength(2000).WithMessage("La descripcion no puede superar 2000 caracteres.");

    /// <summary>
    /// Las fechas son horas de pared del estudiante: una fecha con zona ("Z" u offset) se rechaza
    /// en vez de convertirla en silencio a otra hora.
    /// </summary>
    public static void FechaFinValida<T>(this IRuleBuilder<T, DateTime> regla) =>
        regla.NotEmpty().WithMessage("La fecha de fin es obligatoria.")
            .Must(fecha => fecha.Kind == DateTimeKind.Unspecified)
            .WithMessage("La fecha de fin debe enviarse sin zona horaria, por ejemplo 2026-09-20T10:00:00.");

    public static void TipoValido<T>(this IRuleBuilder<T, TipoActividad> regla) =>
        regla.IsInEnum().WithMessage("El tipo de actividad no es valido.");

    public static void ActividadPadreIdValido<T>(this IRuleBuilder<T, string?> regla) =>
        regla.Must(id => id is null || IdFormat.EsValido(id)).WithMessage(IdFormat.Mensaje);
}
