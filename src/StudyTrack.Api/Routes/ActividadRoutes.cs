using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Extensions;
using StudyTrack.Api.Validators;

namespace StudyTrack.Api.Routes;

/// <summary>
/// El usuario que hace la solicitud siempre se llama <c>userId</c>: en la query string para las
/// operaciones sin cuerpo (GET y DELETE) y en el cuerpo JSON para POST y PUT. No se lee ningun header.
/// </summary>
public static class ActividadRoutes
{
    private const string Ruta = "/actividades";
    private const string IdParametro = "actividadId";

    public static IEndpointRouteBuilder MapActividadRoutes(this IEndpointRouteBuilder app)
    {
        var actividades = app.MapGroup(Ruta).WithTags("Actividades");

        actividades.MapGet("/", GetAllAsync)
            .WithName("ObtenerActividades")
            .WithSummary("Lista las actividades del usuario como arbol")
            .WithDescription("Devuelve las actividades propias, las compartidas con el usuario y las subactividades de estas. " +
                             "Una subactividad compartida cuyo padre no es visible aparece como raiz.")
            .Produces<IReadOnlyList<Actividad>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AddEndpointFilter<UserIdQueryFilter>();

        actividades.MapGet("/calendario", GetCalendarioAsync)
            .WithName("ObtenerCalendario")
            .WithSummary("Actividades del usuario que se traslapan con un rango de fechas")
            .WithDescription("desde y hasta deben incluir zona horaria (Z u offset). Una hija cuyo padre tambien esta en el rango " +
                             "aparece solo anidada, sin duplicarse.")
            .Produces<IReadOnlyList<Actividad>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter<UserIdQueryFilter>();

        actividades.MapGet($"/{{{IdParametro}}}", GetByIdAsync)
            .WithName("ObtenerActividadPorId")
            .WithSummary("Obtiene una actividad con su subarbol")
            .Produces<Actividad>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<UserIdQueryFilter>();

        actividades.MapPost("/", CreateAsync)
            .WithName("CrearActividad")
            .WithSummary("Crea una actividad o subactividad")
            .WithDescription("userId es el dueno. Id, fechaInicio, completada, eliminada y demas campos del servidor se ignoran. " +
                             "colaboradores acepta [\"usuario-2\"] o [{\"usuarioId\":\"usuario-2\"}] (usuarios registrados).")
            .Accepts<Actividad>("application/json")
            .Produces<Actividad>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new ValidationFilter<Actividad>(ActividadValidator.RuleSetCreate));

        actividades.MapPut($"/{{{IdParametro}}}", UpdateAsync)
            .WithName("ActualizarActividad")
            .WithSummary("Actualiza una actividad")
            .WithDescription("userId es quien solicita el cambio (dueno o colaborador). Si se envia colaboradores, " +
                             "reemplaza la lista y solo el dueno puede hacerlo; si se omite, no se modifican.")
            .Accepts<Actividad>("application/json")
            .Produces<Actividad>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter(new ValidationFilter<Actividad>(ActividadValidator.RuleSetUpdate));

        actividades.MapDelete($"/{{{IdParametro}}}", DeleteAsync)
            .WithName("EliminarActividad")
            .WithSummary("Elimina logicamente una actividad y su subarbol")
            .WithDescription("Solo el dueno (userId en la query string) puede eliminarla.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<UserIdQueryFilter>();

        actividades.MapPost($"/{{{IdParametro}}}/compartir", CompartirAsync)
            .WithName("CompartirActividad")
            .WithSummary("Comparte una actividad con otro usuario registrado")
            .WithDescription("userId es el dueno que comparte; usuarioId es el destinatario.")
            .Accepts<CompartirActividadRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<ValidationFilter<CompartirActividadRequest>>();

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        [FromQuery, Required] string? userId, IActividadDelegate actividadDelegate, CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.GetAllAsync(userId!, cancellationToken));

    private static async Task<IResult> GetCalendarioAsync(
        [FromQuery, Required] string? desde,
        [FromQuery, Required] string? hasta,
        [FromQuery, Required] string? userId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>();
        if (FechaConsulta.TryParseUtc(desde, out var desdeUtc) is { } errorDesde)
            errores["desde"] = [errorDesde];
        if (FechaConsulta.TryParseUtc(hasta, out var hastaUtc) is { } errorHasta)
            errores["hasta"] = [errorHasta];

        if (errores.Count > 0)
            return ProblemDetailsResponses.ValidationFailed(errores);

        return Results.Ok(await actividadDelegate.GetCalendarioAsync(desdeUtc, hastaUtc, userId!, cancellationToken));
    }

    private static async Task<IResult> GetByIdAsync(
        string actividadId,
        [FromQuery, Required] string? userId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var actividad = await actividadDelegate.GetByIdAsync(actividadId, userId!, cancellationToken);

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

    private static async Task<IResult> UpdateAsync(
        string actividadId, Actividad actividad, IActividadDelegate actividadDelegate, CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.UpdateAsync(actividadId, actividad, actividad.UserId, cancellationToken));

    private static async Task<IResult> DeleteAsync(
        string actividadId,
        [FromQuery, Required] string? userId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        await actividadDelegate.DeleteAsync(actividadId, userId!, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CompartirAsync(
        string actividadId,
        CompartirActividadRequest request,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        await actividadDelegate.CompartirAsync(actividadId, request.UsuarioId, request.UserId, cancellationToken);
        return Results.Ok(new { mensaje = $"Actividad compartida exitosamente con '{request.UsuarioId}'." });
    }
}
