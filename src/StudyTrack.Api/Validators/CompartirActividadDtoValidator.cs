using FluentValidation;
using StudyTrack.Api.Dtos;

namespace StudyTrack.Api.Validators;

public class CompartirActividadDtoValidator : AbstractValidator<CompartirActividadDto>
{
    public CompartirActividadDtoValidator()
    {
        RuleFor(x => x.UsuarioId)
            .NotEmpty().WithMessage("El usuarioId es obligatorio.")
            .MaximumLength(64).WithMessage("El usuarioId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            .When(x => !string.IsNullOrEmpty(x.UsuarioId), ApplyConditionTo.CurrentValidator);
    }
}
