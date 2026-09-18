using Taxonomy.Api.Data;
using Taxonomy.Api.Endpoints;
using Taxonomy.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Taxonomy")
    ?? throw new InvalidOperationException("Connection string 'Taxonomy' is not configured.");

builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddSingleton<TaxonomyRepository>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RequestAbortedExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/api/health");
app.MapTaxonomy();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
