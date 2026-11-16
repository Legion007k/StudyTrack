using StudyTrack.Api.Delegates.Exceptions;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Api.Delegates;

public class ActividadDelegate(IActividadRepository repository, TimeProvider timeProvider) : IActividadDelegate
{
    public async Task<IReadOnlyList<Actividad>> GetAllAsync(string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var actividades = await repository.GetAllAsync(callerUserId, cancellationToken);

        // El arbol se arma desde la lista plana, sin depender de que las navegaciones vengan cargadas.
        var hijasPorPadre = actividades
            .Where(a => a.ActividadPadreId is not null)
            .ToLookup(a => a.ActividadPadreId!);

        var raices = OrdenadasParaMostrar(actividades.Where(a => a.ActividadPadreId is null)).ToList();
        foreach (var raiz in raices)
        {
            AsignarHijas(raiz, hijasPorPadre, nivel: 0);
        }

        return raices;
    }

    public async Task<IReadOnlyList<Actividad>> GetCalendarioAsync(
        DateTime desde, DateTime hasta, string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var desdeUtc = AUtc(desde);
        var hastaUtc = AUtc(hasta);

        if (desdeUtc > hastaUtc)
            throw new BusinessRuleException("rango", "La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");

        var actividades = await repository.GetPorRangoFechasAsync(desdeUtc, hastaUtc, callerUserId, cancellationToken);

        var hijasPorPadre = actividades
            .Where(a => a.ActividadPadreId is not null)
            .ToLookup(a => a.ActividadPadreId!);

        var idsEnRango = actividades.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var raices = OrdenadasParaMostrar(
            actividades.Where(a => a.ActividadPadreId is null || !idsEnRango.Contains(a.ActividadPadreId)))
            .ToList();

        foreach (var raiz in raices)
        {
            AsignarHijas(raiz, hijasPorPadre, nivel: 0);
        }

        return raices;
    }

    public async Task<Actividad?> GetByIdAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken);
        if (actividad is null)
            return null;

        if (callerUserId is not null && !actividad.EsAccesiblePor(callerUserId))
            throw new ForbiddenException("No tiene autorizacion para acceder a esta actividad.");

        OrdenarHijasRecursivo(actividad);
        return actividad;
    }

    public async Task<string> CreateAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        actividad.FechaInicio = Ahora();
        actividad.FechaFin = AUtc(actividad.FechaFin);

        ValidarRangoDeFechas(actividad);
        await ValidarDuplicadoAsync(actividad, excludeId: null, cancellationToken);

        if (actividad.ActividadPadreId is not null)
        {
            await ValidarPadreAsync(actividad, actividad.ActividadPadreId, cancellationToken);
        }

        if (actividad.Colaboradores is not null && actividad.Colaboradores.Count > 0)
        {
            await ProcesarColaboradoresCreacionAsync(actividad, cancellationToken);
        }
        else
        {
            actividad.Colaboradores = new List<ActividadCompartida>();
        }

        await repository.AddAsync(actividad, cancellationToken);
        return actividad.Id;
    }

    public async Task<Actividad> UpdateAsync(string id, Actividad actividadActualizada, string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (callerUserId is not null && !actividad.EsAccesiblePor(callerUserId))
            throw new ForbiddenException("No tiene autorizacion para modificar esta actividad.");

        actividad.Titulo = actividadActualizada.Titulo;
        actividad.Descripcion = actividadActualizada.Descripcion;
        actividad.FechaFin = AUtc(actividadActualizada.FechaFin);
        actividad.Tipo = actividadActualizada.Tipo;

        ValidarRangoDeFechas(actividad);
        await ValidarDuplicadoAsync(actividad, excludeId: actividad.Id, cancellationToken);

        if (actividadActualizada.ActividadPadreId != actividad.ActividadPadreId)
        {
            if (actividadActualizada.ActividadPadreId is not null)
                await ValidarPadreAsync(actividad, actividadActualizada.ActividadPadreId, cancellationToken);

            actividad.ActividadPadreId = actividadActualizada.ActividadPadreId;
        }

        // Una raiz decide la profundidad de su arbol: cambiarle el tipo no puede dejar hijas fuera del limite.
        if (actividad.ActividadPadreId is null && !Actividad.CabeEnProfundidad(actividad.Tipo, 0, actividad.Altura()))
        {
            throw new BusinessRuleException("tipo",
                $"Una actividad raiz de tipo {actividad.Tipo} admite como maximo " +
                $"{Actividad.ProfundidadMaxima(actividad.Tipo)} niveles de subactividades y esta tiene {actividad.Altura()}.");
        }

        if (actividadActualizada.Completada)
            actividad.MarcarCompletada(Ahora());
        else
            actividad.MarcarPendiente();

        if (actividadActualizada.Colaboradores is not null)
        {
            if (callerUserId is not null && !actividad.EsPropietario(callerUserId))
            {
                throw new ForbiddenException("Solo el propietario puede modificar los colaboradores de la actividad.");
            }

            await ProcesarColaboradoresActualizacionAsync(actividad, actividadActualizada.Colaboradores, cancellationToken);
        }

        await repository.UpdateAsync(actividad, cancellationToken);
        OrdenarHijasRecursivo(actividad);
        return actividad;
    }

    public async Task CompartirAsync(
        string id, string usuarioIdDestino, string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (string.IsNullOrWhiteSpace(callerUserId) || !actividad.EsPropietario(callerUserId))
            throw new ForbiddenException("Solo el propietario puede compartir la actividad con otros usuarios.");

        if (usuarioIdDestino == actividad.UserId)
            throw new BusinessRuleException("usuarioId", "No se puede compartir la actividad con su propio creador.");

        if (!await repository.ExisteUsuarioAsync(usuarioIdDestino, cancellationToken))
            throw new BadRequestException("usuarioId", $"El usuario destinatario '{usuarioIdDestino}' no existe.");

        await repository.CompartirAsync(actividad, usuarioIdDestino, Ahora(), cancellationToken);
    }

    public async Task DeleteAsync(string id, string? callerUserId = null, CancellationToken cancellationToken = default)
    {
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (string.IsNullOrWhiteSpace(callerUserId) || !actividad.EsPropietario(callerUserId))
            throw new ForbiddenException("Solo el propietario puede eliminar la actividad. No se permite eliminar actividades sin especificar un usuario.");

        // El repositorio recibe la actividad con su subarbol cargado y elimina tambien a las descendientes.
        await repository.DeleteAsync(actividad, cancellationToken);
    }

    /// <summary>Hora UTC actual: instante absoluto para evitar desfases de zona horaria.</summary>
    private DateTime Ahora() => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime AUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

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

    private static void AsignarHijas(Actividad actividad, ILookup<string, Actividad> hijasPorPadre, int nivel)
    {
        if (nivel >= Actividad.ProfundidadMaximaAbsoluta)
        {
            actividad.ActividadesHijas = [];
            return;
        }

        var hijas = OrdenadasParaMostrar(hijasPorPadre[actividad.Id]).ToList();
        foreach (var hija in hijas)
        {
            AsignarHijas(hija, hijasPorPadre, nivel + 1);
        }
        actividad.ActividadesHijas = hijas;
    }

    private static void OrdenarHijasRecursivo(Actividad actividad)
    {
        if (actividad.ActividadesHijas.Count == 0)
            return;

        actividad.ActividadesHijas = OrdenadasParaMostrar(actividad.ActividadesHijas).ToList();
        foreach (var hija in actividad.ActividadesHijas)
        {
            OrdenarHijasRecursivo(hija);
        }
    }

    private async Task ProcesarColaboradoresCreacionAsync(Actividad actividad, CancellationToken cancellationToken)
    {
        var procesados = new List<ActividadCompartida>();
        var ahora = Ahora();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in actividad.Colaboradores!)
        {
            var usuarioId = c.UsuarioId?.Trim();
            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                throw new BadRequestException("colaboradores", "El identificador del colaborador no puede estar vacio.");
            }

            if (usuarioId == actividad.UserId)
            {
                throw new BusinessRuleException("colaboradores", "No se puede agregar al creador de la actividad como colaborador.");
            }

            if (!await repository.ExisteUsuarioAsync(usuarioId, cancellationToken))
            {
                throw new BadRequestException("colaboradores", $"El colaborador '{usuarioId}' no existe en el sistema.");
            }

            if (vistos.Add(usuarioId))
            {
                procesados.Add(new ActividadCompartida
                {
                    ActividadId = actividad.Id,
                    UsuarioId = usuarioId,
                    FechaCompartida = ahora
                });
            }
        }

        actividad.Colaboradores = procesados;
    }

    private async Task ProcesarColaboradoresActualizacionAsync(
        Actividad actividad, ICollection<ActividadCompartida> nuevosColaboradores, CancellationToken cancellationToken)
    {
        actividad.Colaboradores ??= new List<ActividadCompartida>();
        var ahora = Ahora();
        var deseados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in nuevosColaboradores)
        {
            var usuarioId = c.UsuarioId?.Trim();
            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                throw new BadRequestException("colaboradores", "El identificador del colaborador no puede estar vacio.");
            }

            if (usuarioId == actividad.UserId)
            {
                throw new BusinessRuleException("colaboradores", "No se puede agregar al creador de la actividad como colaborador.");
            }

            if (!await repository.ExisteUsuarioAsync(usuarioId, cancellationToken))
            {
                throw new BadRequestException("colaboradores", $"El colaborador '{usuarioId}' no existe en el sistema.");
            }

            deseados.Add(usuarioId);
        }

        var paraRemover = actividad.Colaboradores
            .Where(c => !deseados.Contains(c.UsuarioId))
            .ToList();

        foreach (var c in paraRemover)
        {
            actividad.Colaboradores.Remove(c);
        }

        var existentes = actividad.Colaboradores.Select(c => c.UsuarioId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var usuarioId in deseados)
        {
            if (!existentes.Contains(usuarioId))
            {
                actividad.Colaboradores.Add(new ActividadCompartida
                {
                    ActividadId = actividad.Id,
                    UsuarioId = usuarioId,
                    FechaCompartida = ahora
                });
            }
        }
    }
}
