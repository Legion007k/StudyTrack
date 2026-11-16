using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Extensions;

namespace StudyTrack.Api.Routes;

public static class ActividadRoutes
{
    private const string Ruta = "/actividades";
    private const string IdParametro = "actividadId";

    public static IEndpointRouteBuilder MapActividadRoutes(this IEndpointRouteBuilder app)
    {
        var actividades = app.MapGroup(Ruta);

        actividades.MapGet("/", GetAllAsync)
            .WithSummary("Obtiene todas las actividades del usuario (raices y subactividades anidadas)")
            .WithDescription("Devuelve las actividades donde el usuario es duenio o colaborador, ordenadas jerarquicamente.")
            .Produces<IReadOnlyList<Actividad>>(StatusCodes.Status200OK);

        actividades.MapGet("/calendario", GetCalendarioAsync)
            .WithSummary("Consulta el calendario de actividades por rango de fechas")
            .WithDescription("Filtra las actividades cuyo periodo se traslapa con el rango [desde, hasta] para el usuario solicitante.")
            .Produces<IReadOnlyList<Actividad>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        actividades.MapGet($"/{{{IdParametro}}}", GetByIdAsync)
            .WithSummary("Obtiene una actividad por su identificador")
            .Produces<Actividad>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        actividades.MapPost("/", CreateAsync)
            .WithSummary("Crea una nueva actividad")
            .WithDescription("Permite crear actividades asociando colaboradores directamente en la misma solicitud atomica.")
            .Produces<Actividad>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new ValidationFilter<Actividad>("Create"));

        actividades.MapPost($"/{{{IdParametro}}}/compartir", CompartirAsync)
            .WithSummary("Comparte una actividad con otro estudiante colaborador")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<ValidationFilter<CompartirActividadRequest>>();

        actividades.MapPut($"/{{{IdParametro}}}", UpdateAsync)
            .WithSummary("Actualiza una actividad existente")
            .WithDescription("Actualiza titulo, fechas, tipo, estado de completado y sincroniza colaboradores si se especifican.")
            .Produces<Actividad>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter(new ValidationFilter<Actividad>("Update"));

        actividades.MapDelete($"/{{{IdParametro}}}", DeleteAsync)
            .WithSummary("Elimina logicamente una actividad (Soft Delete)")
            .WithDescription("Solo el duenio puede eliminar la actividad. Si tiene subactividades, se eliminan logicamente en cascada.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        [FromQuery] string? userId,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var callerUserId = !string.IsNullOrWhiteSpace(userId) ? userId : headerUserId;
        return Results.Ok(await actividadDelegate.GetAllAsync(callerUserId, cancellationToken));
    }

    private static async Task<IResult> GetCalendarioAsync(
        DateTime? desde,
        DateTime? hasta,
        [FromQuery] string? userId,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        if (!desde.HasValue || !hasta.HasValue)
        {
            return ProblemDetailsResponses.ValidationFailed(
                !desde.HasValue ? "desde" : "hasta",
                "Los parametros de consulta 'desde' y 'hasta' son obligatorios para la vista de calendario.");
        }

        var callerUserId = !string.IsNullOrWhiteSpace(userId) ? userId : headerUserId;
        var actividades = await actividadDelegate.GetCalendarioAsync(desde.Value, hasta.Value, callerUserId, cancellationToken);
        return Results.Ok(actividades);
    }

    private static async Task<IResult> GetByIdAsync(
        string actividadId,
        [FromQuery] string? userId,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var callerUserId = !string.IsNullOrWhiteSpace(userId) ? userId : headerUserId;
        var actividad = await actividadDelegate.GetByIdAsync(actividadId, callerUserId, cancellationToken);

        return actividad is null
            ? ProblemDetailsResponses.NotFound($"No existe una actividad con id '{actividadId}'.")
            : Results.Ok(actividad);
    }

    private static async Task<IResult> CreateAsync(
        Actividad actividad, IActividadDelegate actividadDelegate, CancellationToken cancellationToken)
    {
        var id = await actividadDelegate.CreateAsync(actividad, cancellationToken);
        var creada = await actividadDelegate.GetByIdAsync(id, actividad.UserId, cancellationToken);

        return Results.Created($"{Ruta}/{id}", creada);
    }

    private static async Task<IResult> CompartirAsync(
        string actividadId,
        CompartirActividadRequest request,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var emisorId = request.ObtenerPropietario() ?? headerUserId;
        await actividadDelegate.CompartirAsync(actividadId, request.UsuarioId, emisorId, cancellationToken);
        return Results.Ok(new { mensaje = $"Actividad compartida exitosamente con '{request.UsuarioId}'." });
    }

    private static async Task<IResult> UpdateAsync(
        string actividadId,
        Actividad actividad,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var callerUserId = !string.IsNullOrWhiteSpace(actividad.UserId) ? actividad.UserId : headerUserId;
        return Results.Ok(await actividadDelegate.UpdateAsync(actividadId, actividad, callerUserId, cancellationToken));
    }

    private static async Task<IResult> DeleteAsync(
        string actividadId,
        HttpContext httpContext,
        [FromQuery] string? queryUserId,
        [FromHeader(Name = "X-User-Id")] string? headerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        string? bodyUserId = null;
        if (httpContext.Request.HasJsonContentType())
        {
            try
            {
                var body = await httpContext.Request.ReadFromJsonAsync<DeleteActividadRequest>(cancellationToken);
                bodyUserId = body?.UserId;
            }
            catch
            {
                // Si el body viene malformado se ignora o se captura en el callerUserId
            }
        }

        var header = !string.IsNullOrWhiteSpace(headerUserId)
            ? headerUserId
            : httpContext.Request.Headers["X-User-Id"].FirstOrDefault();

        var callerUserId = !string.IsNullOrWhiteSpace(bodyUserId)
            ? bodyUserId
            : (!string.IsNullOrWhiteSpace(queryUserId) ? queryUserId : header);

        await actividadDelegate.DeleteAsync(actividadId, callerUserId, cancellationToken);
        return Results.NoContent();
    }
}
