using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace StudyTrack.Api.Extensions;

public static class ApiApplicationBuilderExtensions
{
    /// <summary>Hace que todos los errores respondan con el formato del contrato.</summary>
    public static IApplicationBuilder UseApiErrorResponses(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();

        // Minimal APIs responde 415 a un Content-Type que no es JSON sin lanzar excepcion y sin cuerpo.
        app.UseStatusCodePages(async context =>
        {
            if (context.HttpContext.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
                await ProblemDetailsResponses.UnsupportedMediaType().ExecuteAsync(context.HttpContext);
        });

        return app;
    }
}
