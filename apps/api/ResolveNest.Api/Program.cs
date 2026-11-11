using Neo4j.Driver;
using ResolveNest.Api.Features.Tickets;
using ResolveNest.Api.Features.AiInvestigations;
using ResolveNest.Api.Features.Ingestion;
using ResolveNest.Api.Features.Investigations;
using ResolveNest.Api.Features.ReplyDrafts;

var isIngestionCommand = IngestionCommandLine.IsInvocation(args);
var builder = WebApplication.CreateBuilder(isIngestionCommand ? [] : args);

// Read Neo4j configuration
var neo4jUri = builder.Configuration["Neo4j:Uri"]
    ?? throw new InvalidOperationException("Neo4j URI is missing.");

var neo4jUsername = builder.Configuration["Neo4j:Username"]
    ?? throw new InvalidOperationException("Neo4j username is missing.");

var neo4jPassword = builder.Configuration["Neo4j:Password"]
    ?? throw new InvalidOperationException("Neo4j password is missing.");

// Register the Neo4j driver
builder.Services.AddSingleton<IDriver>(_ =>
    GraphDatabase.Driver(
        neo4jUri,
        AuthTokens.Basic(neo4jUsername, neo4jPassword)
    )
);

builder.Services.AddIngestion(builder.Configuration);
builder.Services.AddInvestigations();
builder.Services.AddTickets();
builder.Services.AddAiInvestigations(builder.Configuration);
builder.Services.AddReplyDrafts();

var app = builder.Build();

if (isIngestionCommand)
{
    var exitCode = await IngestionCommandLine.ExecuteAsync(app.Services, args);
    await app.DisposeAsync();
    return exitCode;
}

// Application health endpoint
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "ResolveNest.Api"
    });
});

// Database connectivity endpoint
app.MapGet("/api/health/db", async (
    IDriver driver,
    ILogger<Program> logger) =>
{
    try
    {
        await driver.VerifyConnectivityAsync();

        return Results.Ok(new
        {
            status = "healthy",
            database = "neo4j"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Neo4j connectivity check failed");

        return Results.Problem(
            title: "Database unavailable",
            statusCode: StatusCodes.Status503ServiceUnavailable
        );
    }
});

app.MapInvestigations();
app.MapTickets();
app.MapAiInvestigations();
app.MapReplyDrafts();

app.Run();
return 0;
