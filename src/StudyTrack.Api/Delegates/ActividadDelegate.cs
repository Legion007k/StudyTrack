using StudyTrack.Api.Delegates.Exceptions;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Api.Delegates;

public class ActividadDelegate(IActividadRepository repository, TimeProvider timeProvider) : IActividadDelegate
{
    public async Task<IReadOnlyList<Actividad>> GetAllAsync(string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var actividades = await repository.GetAllAsync(callerUserId, cancellationToken);
        return ArmarArbol(actividades);
    }

    public async Task<IReadOnlyList<Actividad>> GetCalendarioAsync(
        DateTime desde, DateTime hasta, string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var desdeUtc = AUtc(desde);
        var hastaUtc = AUtc(hasta);

        if (desdeUtc > hastaUtc)
            throw new BusinessRuleException("rango", "La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");

        var actividades = await repository.GetPorRangoFechasAsync(desdeUtc, hastaUtc, callerUserId, cancellationToken);
        return ArmarArbol(actividades);
    }

    public async Task<Actividad?> GetByIdAsync(string id, string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var actividad = await repository.GetByIdAsync(id, cancellationToken);
        if (actividad is null)
            return null;

        if (!await PuedeAccederAsync(actividad, callerUserId, cancellationToken))
            throw new ForbiddenException("No tiene autorizacion para acceder a esta actividad.");

        OrdenarHijasRecursivo(actividad);
        return actividad;
    }

    public async Task<string> CreateAsync(Actividad actividad, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(actividad.UserId);

        // Los campos que controla el servidor se fijan aqui, sin importar lo que haya enviado el cliente.
        actividad.Id = Guid.NewGuid().ToString();
        actividad.FechaInicio = Ahora();
        actividad.FechaFin = AUtc(actividad.FechaFin);
        actividad.Completada = false;
        actividad.FechaCompletada = null;
        actividad.GeneradaAutomaticamente = false;
        actividad.Eliminada = false;
        actividad.FechaEliminacion = null;
        actividad.ActividadPadre = null;
        actividad.ActividadesHijas = new List<Actividad>();

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

    public async Task<Actividad> UpdateAsync(
        string id, Actividad actividadActualizada, string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (!await PuedeAccederAsync(actividad, callerUserId, cancellationToken))
            throw new ForbiddenException("No tiene autorizacion para modificar esta actividad.");

        // Se revisa antes de tocar cualquier campo para no dejar cambios a medias.
        if (actividadActualizada.Colaboradores is not null && !actividad.EsPropietario(callerUserId))
            throw new ForbiddenException("Solo el propietario puede modificar los colaboradores de la actividad.");

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
            await ProcesarColaboradoresActualizacionAsync(actividad, actividadActualizada.Colaboradores, cancellationToken);
        }

        await repository.UpdateAsync(actividad, cancellationToken);
        OrdenarHijasRecursivo(actividad);
        return actividad;
    }

    public async Task CompartirAsync(
        string id, string usuarioIdDestino, string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (!actividad.EsPropietario(callerUserId))
            throw new ForbiddenException("Solo el propietario puede compartir la actividad con otros usuarios.");

        if (usuarioIdDestino == actividad.UserId)
            throw new BusinessRuleException("usuarioId", "No se puede compartir la actividad con su propio creador.");

        if (!await repository.ExisteUsuarioAsync(usuarioIdDestino, cancellationToken))
            throw new BadRequestException("usuarioId", $"El usuario destinatario '{usuarioIdDestino}' no existe.");

        await repository.CompartirAsync(actividad, usuarioIdDestino, Ahora(), cancellationToken);
    }

    public async Task DeleteAsync(string id, string callerUserId, CancellationToken cancellationToken = default)
    {
        ExigirUsuario(callerUserId);
        var actividad = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No existe una actividad con id '{id}'.");

        if (!actividad.EsPropietario(callerUserId))
            throw new ForbiddenException("Solo el propietario puede eliminar la actividad.");

        // El repositorio recibe la actividad con su subarbol cargado y elimina tambien a las descendientes.
        await repository.DeleteAsync(actividad, Ahora(), cancellationToken);
    }

    /// <summary>Hora UTC actual: instante absoluto para evitar desfases de zona horaria.</summary>
    private DateTime Ahora() => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Convierte a UTC. La API rechaza fechas sin zona antes de llegar aqui; si aun asi llega una
    /// (por ejemplo desde otra capa), se toma como UTC.
    /// </summary>
    private static DateTime AUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

    /// <summary>La capa HTTP ya valida el usuario; esto protege a otros llamadores del delegate.</summary>
    private static void ExigirUsuario(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("userId", "El userId es obligatorio.");
    }

    /// <summary>
    /// El usuario puede ver o editar la actividad si es suya, si se la compartieron o si cuelga
    /// de una actividad compartida con el.
    /// </summary>
    private async Task<bool> PuedeAccederAsync(Actividad actividad, string userId, CancellationToken cancellationToken)
    {
        var actual = actividad;
        for (var nivel = 0; nivel <= Actividad.ProfundidadMaximaAbsoluta; nivel++)
        {
            if (actual.EsAccesiblePor(userId))
                return true;

            if (actual.ActividadPadreId is null)
                return false;

            var padre = await repository.GetByIdAsync(actual.ActividadPadreId, cancellationToken);
            if (padre is null)
                return false;

            actual = padre;
        }

        return false;
    }

    /// <summary>
    /// Arma el arbol desde una lista plana. Es raiz toda actividad cuyo padre no esta en la lista:
    /// asi una subactividad compartida (o en rango, en el calendario) aparece aunque su padre no,
    /// y una hija cuyo padre si esta solo aparece anidada, sin duplicarse en el nivel superior.
    /// </summary>
    private static IReadOnlyList<Actividad> ArmarArbol(IReadOnlyList<Actividad> actividades)
    {
        var hijasPorPadre = actividades
            .Where(a => a.ActividadPadreId is not null)
            .ToLookup(a => a.ActividadPadreId!);

        var ids = actividades.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var raices = OrdenadasParaMostrar(
            actividades.Where(a => a.ActividadPadreId is null || !ids.Contains(a.ActividadPadreId)))
            .ToList();

        foreach (var raiz in raices)
        {
            AsignarHijas(raiz, hijasPorPadre, nivel: 0);
        }

        return raices;
    }

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

    /// <summary>
    /// Valida los colaboradores de una solicitud y devuelve sus Ids sin repetir. Los Ids distinguen
    /// mayusculas igual que la base de datos.
    /// </summary>
    private async Task<IReadOnlyList<string>> ValidarColaboradoresAsync(
        Actividad actividad, IEnumerable<ActividadCompartida?> colaboradores, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var c in colaboradores)
        {
            var usuarioId = c?.UsuarioId?.Trim();
            if (string.IsNullOrWhiteSpace(usuarioId))
                throw new BadRequestException("colaboradores", "El identificador del colaborador no puede estar vacio.");

            if (usuarioId == actividad.UserId)
                throw new BusinessRuleException("colaboradores", "No se puede agregar al creador de la actividad como colaborador.");

            if (!vistos.Add(usuarioId))
                continue;

            if (!await repository.ExisteUsuarioAsync(usuarioId, cancellationToken))
                throw new BadRequestException("colaboradores", $"El colaborador '{usuarioId}' no existe en el sistema.");

            ids.Add(usuarioId);
        }

        return ids;
    }

    private async Task ProcesarColaboradoresCreacionAsync(Actividad actividad, CancellationToken cancellationToken)
    {
        var ids = await ValidarColaboradoresAsync(actividad, actividad.Colaboradores!, cancellationToken);
        var ahora = Ahora();

        actividad.Colaboradores = ids
            .Select(usuarioId => new ActividadCompartida { ActividadId = actividad.Id, UsuarioId = usuarioId, FechaCompartida = ahora })
            .ToList();
    }

    private async Task ProcesarColaboradoresActualizacionAsync(
        Actividad actividad, ICollection<ActividadCompartida> nuevosColaboradores, CancellationToken cancellationToken)
    {
        var deseados = (await ValidarColaboradoresAsync(actividad, nuevosColaboradores, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        actividad.Colaboradores ??= new List<ActividadCompartida>();
        foreach (var c in actividad.Colaboradores.Where(c => !deseados.Contains(c.UsuarioId)).ToList())
        {
            actividad.Colaboradores.Remove(c);
        }

        var ahora = Ahora();
        var existentes = actividad.Colaboradores.Select(c => c.UsuarioId).ToHashSet(StringComparer.Ordinal);
        foreach (var usuarioId in deseados.Where(id => !existentes.Contains(id)))
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
