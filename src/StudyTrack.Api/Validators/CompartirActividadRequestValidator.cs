using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

public class CompartirActividadRequestValidator : AbstractValidator<CompartirActividadRequest>
{
    public CompartirActividadRequestValidator()
    {
        RuleFor(x => x.UsuarioId)
            .NotEmpty().WithMessage("El usuarioId es obligatorio.")
            .MaximumLength(64).WithMessage("El usuarioId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            .When(x => !string.IsNullOrEmpty(x.UsuarioId), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.PropietarioId)
            .MaximumLength(64).WithMessage("El propietarioId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            .When(x => !string.IsNullOrEmpty(x.PropietarioId), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.UserId)
            .MaximumLength(64).WithMessage("El userId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            .When(x => !string.IsNullOrEmpty(x.UserId), ApplyConditionTo.CurrentValidator);
    }
}
