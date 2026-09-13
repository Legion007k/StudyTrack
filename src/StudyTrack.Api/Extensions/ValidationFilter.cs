using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StudyTrack.Api.Extensions;

/// <summary>Valida con FluentValidation el argumento de tipo <typeparamref name="T"/> del endpoint.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argumento = context.Arguments.OfType<T>().FirstOrDefault();
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();

        if (argumento is null || validator is null)
            return await next(context);

        var resultado = await validator.ValidateAsync(argumento, context.HttpContext.RequestAborted);
        if (resultado.IsValid)
            return await next(context);

        // Las claves usan el mismo nombre que el campo en JSON ("fechaFin", no "FechaFin").
        var errores = resultado.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(error => error.ErrorMessage).Distinct().ToArray());

        return ProblemDetailsResponses.ValidationFailed(errores);
    }
}
