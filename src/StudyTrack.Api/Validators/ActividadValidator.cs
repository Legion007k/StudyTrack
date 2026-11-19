using FluentValidation;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Validators;

/// <summary>
/// Valida el cuerpo de POST y PUT /actividades. En POST el userId es el dueno de la nueva actividad;
/// en PUT es el usuario que solicita el cambio. En ambos casos es obligatorio.
/// </summary>
public class ActividadValidator : AbstractValidator<Actividad>
{
    public const string RuleSetCreate = "Create";
    public const string RuleSetUpdate = "Update";

    public ActividadValidator()
    {
        RuleSet(RuleSetCreate, () =>
        {
            // Las subactividades se crean una por una con actividadPadreId, para que cada una pase
            // por las reglas de dueno, fechas, duplicados y profundidad.
            RuleFor(x => x.ActividadesHijas)
                .Must(hijas => hijas is null || hijas.Count == 0)
                .WithMessage("No se pueden crear subactividades anidadas: crea cada una con POST /actividades indicando actividadPadreId.");
        });

        RuleFor(x => x.UserId).UsuarioIdValido("userId");
        RuleFor(x => x.Titulo).TituloValido();
        RuleFor(x => x.Descripcion).DescripcionValida();
        RuleFor(x => x.FechaFin).FechaFinValida();
        RuleFor(x => x.Tipo).TipoValido();
        RuleFor(x => x.ActividadPadreId).ActividadPadreIdValido();

        RuleForEach(x => x.Colaboradores)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Un colaborador no puede ser null.")
            .Must(c => !string.IsNullOrWhiteSpace(c.UsuarioId)).WithMessage("El identificador del colaborador es obligatorio.")
            .Must(c => IdFormat.EsValido(c.UsuarioId)).WithMessage(IdFormat.Mensaje)
            .OverridePropertyName("colaboradores");
    }
}
