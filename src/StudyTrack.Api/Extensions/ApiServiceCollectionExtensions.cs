using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StudyTrack.Api.Domain;
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
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            // Los enums viajan como texto ("Examen"); un numero se rechaza en vez de aceptarse en silencio.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
            options.SerializerOptions.Converters.Add(new ActividadCompartidaJsonConverter());
        });

        // Por defecto solo se lanza en Development; asi el cuerpo invalido responde igual en todos los entornos.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddValidatorsFromAssemblyContaining<ActividadValidator>(includeInternalTypes: false);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "StudyTrack API",
                Version = "v1",
                Description = "API REST de Gestion de Actividades Academicas con Soft Delete, Autorizacion y Colaboracion en Tiempo Real."
            });
        });

        return services;
    }
}
