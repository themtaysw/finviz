using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;

namespace Taxonomy.Api.Infrastructure;

internal static class OpenApiConfiguration
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "ImageNet Taxonomy API";
            return Task.CompletedTask;
        });

        // The generator ignores [AllowedValues]; clients need them as an enum.
        options.AddSchemaTransformer((schema, context, _) =>
        {
            var allowed = (context.ParameterDescription?.ParameterDescriptor as IParameterInfoParameterDescriptor)
                ?.ParameterInfo.GetCustomAttribute<AllowedValuesAttribute>();

            if (allowed is not null)
            {
                schema.Enum = [.. allowed.Values.OfType<string>().Select(value => (JsonNode)value)];
            }

            return Task.CompletedTask;
        });
    }
}
