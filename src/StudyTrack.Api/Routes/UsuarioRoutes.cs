using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Extensions;

namespace StudyTrack.Api.Routes;

/// <summary>
/// Registro de usuarios. Solo los usuarios registrados pueden recibir actividades compartidas
/// o ser colaboradores. No hay autenticacion: el usuario se identifica con su id.
/// </summary>
public static class UsuarioRoutes
{
    private const string Ruta = "/usuarios";
    private const string IdParametro = "usuarioId";

    public static IEndpointRouteBuilder MapUsuarioRoutes(this IEndpointRouteBuilder app)
    {
        var usuarios = app.MapGroup(Ruta).WithTags("Usuarios");

        usuarios.MapPost("/", RegistrarAsync)
            .WithName("RegistrarUsuario")
            .WithSummary("Registra un usuario")
            .WithDescription("id debe ser unico; con el mismo id se responde 409.")
            .Accepts<Usuario>("application/json")
            .Produces<Usuario>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .AddEndpointFilter<ValidationFilter<Usuario>>();

        usuarios.MapGet($"/{{{IdParametro}}}", GetByIdAsync)
            .WithName("ObtenerUsuario")
            .WithSummary("Obtiene un usuario registrado")
            .Produces<Usuario>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(new IdFormatFilter(IdParametro));

        return app;
    }

    private static async Task<IResult> RegistrarAsync(
        Usuario usuario, IUsuarioDelegate usuarioDelegate, CancellationToken cancellationToken)
    {
        var registrado = await usuarioDelegate.RegistrarAsync(usuario, cancellationToken);
        return Results.Created($"{Ruta}/{registrado.Id}", registrado);
    }

    private static async Task<IResult> GetByIdAsync(
        string usuarioId, IUsuarioDelegate usuarioDelegate, CancellationToken cancellationToken)
    {
        var usuario = await usuarioDelegate.GetByIdAsync(usuarioId, cancellationToken);

        return usuario is null
            ? ProblemDetailsResponses.NotFound($"No existe un usuario con id '{usuarioId}'.")
            : Results.Ok(usuario);
    }
}
