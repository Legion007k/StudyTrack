using FluentValidation;
using StudyTrack.Api.Dtos;

namespace StudyTrack.Api.Validators;

public class CreateActividadDtoValidator : AbstractValidator<CreateActividadDto>
{
    public CreateActividadDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El userId es obligatorio.")
            .MaximumLength(64).WithMessage("El userId no puede superar 64 caracteres.")
            .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
            // Solo condiciona la regla de formato; sin CurrentValidator tambien apagaria NotEmpty.
            .When(x => !string.IsNullOrEmpty(x.UserId), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.Titulo).TituloValido();
        RuleFor(x => x.Descripcion).DescripcionValida();
        RuleFor(x => x.FechaFin).FechaFinValida();
        RuleFor(x => x.Tipo).TipoValido();
        RuleFor(x => x.ActividadPadreId).ActividadPadreIdValido();
    }
}
