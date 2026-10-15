var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var environment =
    Environment.GetEnvironmentVariable("APP_ENVIRONMENT")
    ?? "development";

var version =
    Environment.GetEnvironmentVariable("APP_VERSION")
    ?? "local";

app.MapGet("/", () => new
{
    application = "DevOps Web API",
    version,
    environment
});

app.MapGet("/health", () => new
{
    status = "healthy",
    version,
    environment,
    timestamp = DateTime.UtcNow
});

app.MapGet("/version", () => new
{
    version,
    environment,
    message = "Nova funcionalidade da versão 1.1"
});

app.Run();
