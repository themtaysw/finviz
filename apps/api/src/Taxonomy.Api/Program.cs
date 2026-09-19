using System.Text.Json.Serialization;
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
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi(OpenApiConfiguration.Configure);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.MapHealthChecks("/api/health");
app.MapTaxonomy();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
