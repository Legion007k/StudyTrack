using FluentValidation;
using StudyTrack.Api.Dtos;

namespace StudyTrack.Api.Validators;

public class UpdateActividadDtoValidator : AbstractValidator<UpdateActividadDto>
{
    public UpdateActividadDtoValidator()
    {
        RuleFor(x => x.Titulo).TituloValido();
        RuleFor(x => x.Descripcion).DescripcionValida();
        RuleFor(x => x.FechaFin).FechaFinValida();
        RuleFor(x => x.Tipo).TipoValido();
        RuleFor(x => x.ActividadPadreId).ActividadPadreIdValido();
    }
}
