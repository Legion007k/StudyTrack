using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StudyTrack.Api.Delegates.Exceptions;

namespace StudyTrack.Api.Extensions;

/// <summary>Traduce las excepciones a respuestas de error del contrato.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var respuesta = exception switch
        {
            // Minimal APIs lanza BadHttpRequestException al leer el cuerpo: JSON malformado
            // o valores que no encajan en el tipo (un enum desconocido, una fecha invalida).
            BadHttpRequestException =>
                ProblemDetailsResponses.ValidationFailed("body", "El cuerpo de la solicitud no es JSON valido o no tiene el formato esperado."),
            NotFoundException notFound =>
                ProblemDetailsResponses.NotFound(notFound.Message),
            BusinessRuleException reglaDeNegocio =>
                ProblemDetailsResponses.BusinessRuleViolation(reglaDeNegocio.Errors),
            _ => null
        };

        if (respuesta is null)
        {
            logger.LogError(exception, "Error no controlado en {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
            respuesta = ProblemDetailsResponses.InternalServerError();
        }

        await respuesta.ExecuteAsync(httpContext);
        return true;
    }
}
