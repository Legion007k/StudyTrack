using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

public class ActividadValidator : AbstractValidator<Actividad>
{
    public ActividadValidator()
    {
        RuleSet("Create", () =>
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("El userId es obligatorio.")
                .MaximumLength(64).WithMessage("El userId no puede superar 64 caracteres.")
                .Must(IdFormat.EsValido).WithMessage(IdFormat.Mensaje)
                .When(x => !string.IsNullOrEmpty(x.UserId), ApplyConditionTo.CurrentValidator);
        });

        RuleFor(x => x.Titulo).TituloValido();
        RuleFor(x => x.Descripcion).DescripcionValida();
        RuleFor(x => x.FechaFin).FechaFinValida();
        RuleFor(x => x.Tipo).TipoValido();
        RuleFor(x => x.ActividadPadreId).ActividadPadreIdValido();
    }
}
