using Microsoft.AspNetCore.Http;
using StudyTrack.Api.Validators;

namespace StudyTrack.Api.Extensions;

/// <summary>
/// Exige el parametro de consulta <c>userId</c> (quien hace la solicitud) con formato de ID valido.
/// Se usa en las operaciones sin cuerpo: consultas y DELETE.
/// </summary>
public sealed class UserIdQueryFilter : IEndpointFilter
{
    public const string Parametro = "userId";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var userId = context.HttpContext.Request.Query[Parametro].ToString();

        if (string.IsNullOrWhiteSpace(userId))
            return ProblemDetailsResponses.ValidationFailed(Parametro, "El parametro de consulta 'userId' es obligatorio.");

        if (userId.Length > 64)
            return ProblemDetailsResponses.ValidationFailed(Parametro, "El userId no puede superar 64 caracteres.");

        if (!IdFormat.EsValido(userId))
            return ProblemDetailsResponses.ValidationFailed(Parametro, IdFormat.Mensaje);

        return await next(context);
    }
}
