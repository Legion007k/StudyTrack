using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StudyTrack.Api.Validators;

namespace StudyTrack.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Serializacion, validacion y manejo de errores de la capa HTTP.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // Los enums viajan como texto ("Examen"); un numero se rechaza en vez de aceptarse en silencio.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        });

        // Por defecto solo se lanza en Development; asi el cuerpo invalido responde igual en todos los entornos.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddValidatorsFromAssemblyContaining<CreateActividadDtoValidator>(includeInternalTypes: false);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
