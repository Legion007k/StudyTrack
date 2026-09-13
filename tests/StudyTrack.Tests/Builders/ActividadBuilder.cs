using StudyTrack.Api.Domain;

namespace StudyTrack.Tests.Builders;

/// <summary>Builder fluido con valores fijos por defecto para que las pruebas sean deterministas.</summary>
public class ActividadBuilder
{
    public static readonly DateTime FechaBase = new(2026, 9, 1, 8, 0, 0);

    private readonly Actividad _actividad = new()
    {
        Id = "actividad-1",
        UserId = "usuario-1",
        Titulo = "Examen de Calculo",
        Descripcion = "Derivadas e integrales",
        FechaInicio = FechaBase,
        FechaFin = FechaBase.AddDays(7),
        Tipo = TipoActividad.General
    };

    public ActividadBuilder WithId(string id) { _actividad.Id = id; return this; }
    public ActividadBuilder WithUserId(string userId) { _actividad.UserId = userId; return this; }
    public ActividadBuilder WithTitulo(string titulo) { _actividad.Titulo = titulo; return this; }
    public ActividadBuilder WithDescripcion(string? descripcion) { _actividad.Descripcion = descripcion; return this; }
    public ActividadBuilder WithFechaInicio(DateTime fechaInicio) { _actividad.FechaInicio = fechaInicio; return this; }
    public ActividadBuilder WithFechaFin(DateTime fechaFin) { _actividad.FechaFin = fechaFin; return this; }
    public ActividadBuilder WithTipo(TipoActividad tipo) { _actividad.Tipo = tipo; return this; }
    public ActividadBuilder WithGeneradaAutomaticamente(bool generada) { _actividad.GeneradaAutomaticamente = generada; return this; }

    public ActividadBuilder WithCompletada(DateTime fechaCompletada)
    {
        _actividad.Completada = true;
        _actividad.FechaCompletada = fechaCompletada;
        return this;
    }

    /// <summary>Enlaza ambos lados de la relacion: la hija apunta al padre y el padre la incluye.</summary>
    public ActividadBuilder WithPadre(Actividad padre)
    {
        _actividad.ActividadPadreId = padre.Id;
        _actividad.ActividadPadre = padre;
        padre.ActividadesHijas.Add(_actividad);
        return this;
    }

    public Actividad Build() => _actividad;
}
