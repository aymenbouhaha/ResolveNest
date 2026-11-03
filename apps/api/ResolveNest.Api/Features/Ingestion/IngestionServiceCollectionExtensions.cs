using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ResolveNest.Api.Features.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SeedIngestionSettings>(configuration.GetSection(SeedIngestionSettings.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GraphInspector>();
        services.AddSingleton<SeedSourceReader>();
        services.AddSingleton<SeedGraphBuilder>();
        services.AddSingleton<SeedGraphWriter>();
        services.AddSingleton<SeedIngestionCommand>();
        services.AddSingleton<KnowledgeSourceReader>();
        services.AddSingleton<KnowledgeGraphBuilder>();
        services.AddSingleton<KnowledgeGraphWriter>();
        services.AddSingleton<KnowledgeIngestionCommand>();
        return services;
    }
}
