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
    /// Las fechas deben ser instantes válidos (preferentemente en UTC).
    /// </summary>
    public static void FechaFinValida<T>(this IRuleBuilder<T, DateTime> regla) =>
        regla.NotEmpty().WithMessage("La fecha de fin es obligatoria.");

    public static void TipoValido<T>(this IRuleBuilder<T, TipoActividad> regla) =>
        regla.IsInEnum().WithMessage("El tipo de actividad no es valido.");

    public static void ActividadPadreIdValido<T>(this IRuleBuilder<T, string?> regla) =>
        regla.Must(id => id is null || IdFormat.EsValido(id)).WithMessage(IdFormat.Mensaje);
}
