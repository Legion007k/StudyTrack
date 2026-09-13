using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace StudyTrack.Api.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5)));

        services.AddScoped<IActividadRepository, ActividadRepository>();

        return services;
    }
}
