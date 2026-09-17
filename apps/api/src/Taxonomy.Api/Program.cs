using Taxonomy.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Taxonomy")
    ?? throw new InvalidOperationException("Connection string 'Taxonomy' is not configured.");

builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/api/health");

app.Run();
