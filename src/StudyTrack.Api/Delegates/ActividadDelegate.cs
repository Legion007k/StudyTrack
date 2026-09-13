using StudyTrack.Api.Delegates.Exceptions;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Dtos;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Api.Delegates;

public class ActividadDelegate(IActividadRepository repository, TimeProvider timeProvider) : IActividadDelegate
{
    public async Task<IReadOnlyList<ActividadDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var actividades = await repository.GetAllAsync(cancellationToken);

        // El arbol se arma desde la lista plana, sin depender de que las navegaciones vengan cargadas.
        var hijasPorPadre = actividades
            .Where(a => a.ActividadPadreId is not null)
            .ToLookup(a => a.ActividadPadreId!);

        return OrdenadasParaMostrar(actividades.Where(a => a.ActividadPadreId is null))
            .Select(raiz => ToDto(raiz, a => hijasPorPadre[a.Id], nivel: 0))
            .ToList();
    }

    public async Task<ActividadDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken);
        return actividad is null ? null : ToDto(actividad, a => a.ActividadesHijas, nivel: 0);
    }

    public async Task<string> CreateAsync(CreateActividadDto dto, CancellationToken cancellationToken = default)
    {
        var actividad = new Actividad
        {
            UserId = dto.UserId,
            Titulo = dto.Titulo,
            Descripcion = dto.Descripcion,
            FechaInicio = Ahora(),
            FechaFin = dto.FechaFin,
            Tipo = dto.Tipo
        };

        ValidarRangoDeFechas(actividad);
        await ValidarDuplicadoAsync(actividad, excludeId: null, cancellationToken);

        if (dto.ActividadPadreId is not null)
        {
            await ValidarPadreAsync(actividad, dto.ActividadPadreId, cancellationToken);
            actividad.ActividadPadreId = dto.ActividadPadreId;
        }

        await repository.AddAsync(actividad, cancellationToken);
        return actividad.Id;
    }

    public async Task<ActividadDto> UpdateAsync(string id, UpdateActividadDto dto, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        actividad.Titulo = dto.Titulo;
        actividad.Descripcion = dto.Descripcion;
        actividad.FechaFin = dto.FechaFin;
        actividad.Tipo = dto.Tipo;

        ValidarRangoDeFechas(actividad);
        await ValidarDuplicadoAsync(actividad, excludeId: actividad.Id, cancellationToken);

        if (dto.ActividadPadreId != actividad.ActividadPadreId)
        {
            if (dto.ActividadPadreId is not null)
                await ValidarPadreAsync(actividad, dto.ActividadPadreId, cancellationToken);

            actividad.ActividadPadreId = dto.ActividadPadreId;
        }

        // Una raiz decide la profundidad de su arbol: cambiarle el tipo no puede dejar hijas fuera del limite.
        if (actividad.ActividadPadreId is null && !Actividad.CabeEnProfundidad(actividad.Tipo, 0, actividad.Altura()))
        {
            throw new BusinessRuleException("tipo",
                $"Una actividad raiz de tipo {actividad.Tipo} admite como maximo " +
                $"{Actividad.ProfundidadMaxima(actividad.Tipo)} niveles de subactividades y esta tiene {actividad.Altura()}.");
        }

        if (dto.Completada)
            actividad.MarcarCompletada(Ahora());
        else
            actividad.MarcarPendiente();

        await repository.UpdateAsync(actividad, cancellationToken);
        return ToDto(actividad, a => a.ActividadesHijas, nivel: 0);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        // El repositorio recibe la actividad con su subarbol cargado y elimina tambien a las descendientes.
        await repository.DeleteAsync(actividad, cancellationToken);
    }

    /// <summary>Hora de pared actual: sin Kind, igual que las fechas que llegan del cliente.</summary>
    private DateTime Ahora() => timeProvider.GetLocalNow().DateTime;

    private static void ValidarRangoDeFechas(Actividad actividad)
    {
        if (!actividad.TieneRangoDeFechasValido())
            throw new BusinessRuleException("fechaFin", "La fecha de fin no puede ser anterior a la fecha de inicio.");
    }

    private async Task ValidarDuplicadoAsync(Actividad actividad, string? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.ExistsDuplicateAsync(actividad.UserId, actividad.Titulo, actividad.FechaFin, excludeId, cancellationToken))
            throw new BusinessRuleException("titulo", "El usuario ya tiene una actividad con el mismo titulo y fecha de fin.");
    }

    private async Task ValidarPadreAsync(Actividad actividad, string padreId, CancellationToken cancellationToken)
    {
        const string campo = "actividadPadreId";

        if (padreId == actividad.Id)
            throw new BusinessRuleException(campo, "Una actividad no puede ser su propia actividad padre.");

        if (actividad.ContieneDescendiente(padreId))
            throw new BusinessRuleException(campo, "La actividad padre no puede ser una de sus propias subactividades.");

        var padre = await repository.GetByIdAsync(padreId, cancellationToken)
            ?? throw new BusinessRuleException(campo, $"No existe una actividad padre con id '{padreId}'.");

        if (padre.UserId != actividad.UserId)
            throw new BusinessRuleException(campo, "La actividad padre pertenece a otro usuario.");

        var (raiz, nivelDelPadre) = await ObtenerRaizAsync(padre, cancellationToken);
        if (!Actividad.CabeEnProfundidad(raiz.Tipo, nivelDelPadre + 1, actividad.Altura()))
        {
            throw new BusinessRuleException(campo,
                $"Se supera la profundidad maxima: una actividad raiz de tipo {raiz.Tipo} " +
                $"admite {Actividad.ProfundidadMaxima(raiz.Tipo)} niveles de subactividades.");
        }
    }

    /// <summary>Sube por la cadena de padres hasta la raiz y devuelve en que nivel esta la actividad.</summary>
    private async Task<(Actividad Raiz, int Nivel)> ObtenerRaizAsync(Actividad actividad, CancellationToken cancellationToken)
    {
        var actual = actividad;
        var nivel = 0;

        // El tope evita un ciclo infinito si los datos llegaran a estar corruptos.
        while (actual.ActividadPadreId is not null && nivel <= Actividad.ProfundidadMaximaAbsoluta)
        {
            var padre = await repository.GetByIdAsync(actual.ActividadPadreId, cancellationToken);
            if (padre is null)
                break;

            actual = padre;
            nivel++;
        }

        return (actual, nivel);
    }

    private static IEnumerable<Actividad> OrdenadasParaMostrar(IEnumerable<Actividad> actividades) =>
        actividades.OrderBy(a => a.FechaInicio).ThenBy(a => a.Id, StringComparer.Ordinal);

    private static ActividadDto ToDto(Actividad actividad, Func<Actividad, IEnumerable<Actividad>> hijasDe, int nivel)
    {
        var hijas = nivel < Actividad.ProfundidadMaximaAbsoluta
            ? OrdenadasParaMostrar(hijasDe(actividad)).Select(hija => ToDto(hija, hijasDe, nivel + 1)).ToList()
            : [];

        return new ActividadDto(
            actividad.Id,
            actividad.UserId,
            actividad.Titulo,
            actividad.Descripcion,
            actividad.FechaInicio,
            actividad.FechaFin,
            actividad.Tipo,
            actividad.Completada,
            actividad.FechaCompletada,
            actividad.GeneradaAutomaticamente,
            actividad.ActividadPadreId,
            hijas);
    }
}
