using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ResolveNest.Api.Features.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddSeedIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SeedIngestionSettings>(configuration.GetSection(SeedIngestionSettings.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SeedSourceReader>();
        services.AddSingleton<SeedGraphBuilder>();
        services.AddSingleton<SeedGraphWriter>();
        services.AddSingleton<SeedIngestionCommand>();
        return services;
    }
}
