using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

/// <summary>Valida el cuerpo de POST /usuarios.</summary>
public class UsuarioValidator : AbstractValidator<Usuario>
{
    public UsuarioValidator()
    {
        RuleFor(x => x.Id).UsuarioIdValido("id");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .MaximumLength(200).WithMessage("El email no puede superar 200 caracteres.")
            .EmailAddress().WithMessage("El email no tiene un formato valido.");
    }
}
