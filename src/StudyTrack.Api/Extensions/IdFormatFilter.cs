using Microsoft.AspNetCore.Http;
using StudyTrack.Api.Validators;

namespace StudyTrack.Api.Extensions;

/// <summary>Rechaza con 400 un ID de ruta con formato invalido antes de llegar al delegate.</summary>
public sealed class IdFormatFilter(string parametroDeRuta) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var id = context.HttpContext.Request.RouteValues[parametroDeRuta] as string;

        if (!IdFormat.EsValido(id))
            return ProblemDetailsResponses.ValidationFailed(parametroDeRuta, IdFormat.Mensaje);

        return await next(context);
    }
}
