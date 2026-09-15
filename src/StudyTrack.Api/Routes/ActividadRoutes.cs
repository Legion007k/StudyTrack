using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Dtos;
using StudyTrack.Api.Extensions;

namespace StudyTrack.Api.Routes;
//nuevo endpoint de calendario para organizacion de actividades.
//tabla de user id que pueda corroborar la autorizacion de la actividad y que pueda ser compartida con otros usuarios.
public static class ActividadRoutes
{
    private const string Ruta = "/actividades";
    private const string IdParametro = "actividadId";

    public static IEndpointRouteBuilder MapActividadRoutes(this IEndpointRouteBuilder app)
    {
        var actividades = app.MapGroup(Ruta);

        actividades.MapGet("/", GetAllAsync);

        actividades.MapGet($"/{{{IdParametro}}}", GetByIdAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        actividades.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateActividadDto>>();

        actividades.MapPut($"/{{{IdParametro}}}", UpdateAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro))
            .AddEndpointFilter<ValidationFilter<UpdateActividadDto>>();

        actividades.MapDelete($"/{{{IdParametro}}}", DeleteAsync)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        return app;
    }

    private static async Task<IResult> GetAllAsync(IActividadDelegate actividadDelegate, CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.GetAllAsync(cancellationToken));

    private static async Task<IResult> GetByIdAsync(
        string actividadId, IActividadDelegate actividadDelegate, CancellationToken cancellationToken)
    {
        var actividad = await actividadDelegate.GetByIdAsync(actividadId, cancellationToken);

        return actividad is null
            ? ProblemDetailsResponses.NotFound($"No existe una actividad con id '{actividadId}'.")
            : Results.Ok(actividad);
    }

    private static async Task<IResult> CreateAsync(
        CreateActividadDto dto, IActividadDelegate actividadDelegate, CancellationToken cancellationToken)
    {
        var id = await actividadDelegate.CreateAsync(dto, cancellationToken);
        var creada = await actividadDelegate.GetByIdAsync(id, cancellationToken);

        return Results.Created($"{Ruta}/{id}", creada);
    }

    private static async Task<IResult> UpdateAsync(
        string actividadId, UpdateActividadDto dto, IActividadDelegate actividadDelegate, CancellationToken cancellationToken) =>
        Results.Ok(await actividadDelegate.UpdateAsync(actividadId, dto, cancellationToken));

    private static async Task<IResult> DeleteAsync(
        string actividadId, IActividadDelegate actividadDelegate, CancellationToken cancellationToken)
    {
        await actividadDelegate.DeleteAsync(actividadId, cancellationToken);
        return Results.NoContent();
    }
}
