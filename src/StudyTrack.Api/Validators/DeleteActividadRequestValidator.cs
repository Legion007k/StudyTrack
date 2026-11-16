using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

public class DeleteActividadRequestValidator : AbstractValidator<DeleteActividadRequest>
{
    public DeleteActividadRequestValidator()
    {
        RuleFor(x => x.UserId)
            .MaximumLength(64).WithMessage("El userId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            .When(x => !string.IsNullOrEmpty(x.UserId), ApplyConditionTo.CurrentValidator);
    }
}

