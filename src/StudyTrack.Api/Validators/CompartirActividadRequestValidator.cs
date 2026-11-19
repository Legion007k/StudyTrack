using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

public class CompartirActividadRequestValidator : AbstractValidator<CompartirActividadRequest>
{
    public CompartirActividadRequestValidator()
    {
        RuleFor(x => x.UserId).UsuarioIdValido("userId");
        RuleFor(x => x.UsuarioId).UsuarioIdValido("usuarioId");
    }
}
