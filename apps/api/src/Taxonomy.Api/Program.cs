using System.Text.Json.Serialization;
using Npgsql;
using Taxonomy.Api.Data;
using Taxonomy.Api.Endpoints;
using Taxonomy.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Taxonomy")
    ?? throw new InvalidOperationException("Connection string 'Taxonomy' is not configured.");

// No Kerberos here: skipping the GSS probe saves a negotiation step and a missing-library error on slim images.
builder.Services.AddNpgsqlDataSource(connectionString, dataSource =>
    dataSource.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable);
builder.Services.AddSingleton<TaxonomyRepository>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RequestAbortedExceptionHandler>();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi(OpenApiConfiguration.Configure);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.UseResponseCompression();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseWebApp();
app.MapHealthChecks("/api/health");
app.MapTaxonomy();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
