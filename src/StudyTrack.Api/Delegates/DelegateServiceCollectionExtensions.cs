using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Api.Delegates;

public static class DelegateServiceCollectionExtensions
{
    /// <summary>Registra los delegates y, por debajo, la capa de repositorios de la que dependen.</summary>
    public static IServiceCollection AddDelegates(this IServiceCollection services, string connectionString)
    {
        services.AddRepositories(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IActividadDelegate, ActividadDelegate>();

        return services;
    }
}
