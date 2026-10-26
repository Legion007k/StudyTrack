using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Dtos;
using StudyTrack.Api.Extensions;

namespace StudyTrack.Api.Routes;

public static class ActividadRoutes
{
    private const string Ruta = "/actividades";
    private const string IdParametro = "actividadId";

    public static IEndpointRouteBuilder MapActividadRoutes(this IEndpointRouteBuilder app)
    {
        var actividades = app.MapGroup(Ruta);

        actividades.MapGet("/", GetAllAsync);
        actividades.MapGet("/calendario", GetCalendarioAsync);

        actividades.MapGet($"/{{{IdParametro}}}", GetByIdAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        actividades.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateActividadDto>>();

        actividades.MapPost($"/{{{IdParametro}}}/compartir", CompartirAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<ValidationFilter<CompartirActividadDto>>();

        actividades.MapPut($"/{{{IdParametro}}}", UpdateAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<ValidationFilter<UpdateActividadDto>>();

        actividades.MapDelete($"/{{{IdParametro}}}", DeleteAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        return app;
    }

    private static async Task<IResult> GetAllAsync(IActividadDelegate actividadDelegate, CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.GetAllAsync(cancellationToken));

    private static async Task<IResult> GetCalendarioAsync(
        DateTime? desde,
        DateTime? hasta,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        if (!desde.HasValue || !hasta.HasValue)
        {
            return ProblemDetailsResponses.ValidationFailed(
                !desde.HasValue ? "desde" : "hasta",
                "Los parametros de consulta 'desde' y 'hasta' son obligatorios para la vista de calendario.");
        }

        var actividades = await actividadDelegate.GetCalendarioAsync(desde.Value, hasta.Value, cancellationToken);
        return Results.Ok(actividades);
    }

    private static async Task<IResult> GetByIdAsync(
        string actividadId,
        [FromHeader(Name = "X-User-Id")] string? callerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        var actividad = await actividadDelegate.GetByIdAsync(actividadId, callerUserId, cancellationToken);

        return actividad is null
            ? ProblemDetailsResponses.NotFound($"No existe una actividad con id '{actividadId}'.")
            : Results.Ok(actividad);
    }

    private static async Task<IResult> CreateAsync(
        CreateActividadDto dto, IActividadDelegate actividadDelegate, CancellationToken cancellationToken)
    {
        var id = await actividadDelegate.CreateAsync(dto, cancellationToken);
        var creada = await actividadDelegate.GetByIdAsync(id, dto.UserId, cancellationToken);

        return Results.Created($"{Ruta}/{id}", creada);
    }

    private static async Task<IResult> CompartirAsync(
        string actividadId,
        CompartirActividadDto dto,
        [FromHeader(Name = "X-User-Id")] string? callerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        await actividadDelegate.CompartirAsync(actividadId, dto.UsuarioId, callerUserId, cancellationToken);
        return Results.Ok(new { mensaje = $"Actividad compartida exitosamente con '{dto.UsuarioId}'." });
    }

    private static async Task<IResult> UpdateAsync(
        string actividadId,
        UpdateActividadDto dto,
        [FromHeader(Name = "X-User-Id")] string? callerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.UpdateAsync(actividadId, dto, callerUserId, cancellationToken));

    private static async Task<IResult> DeleteAsync(
        string actividadId,
        [FromHeader(Name = "X-User-Id")] string? callerUserId,
        IActividadDelegate actividadDelegate,
        CancellationToken cancellationToken)
    {
        await actividadDelegate.DeleteAsync(actividadId, callerUserId, cancellationToken);
        return Results.NoContent();
    }
}
