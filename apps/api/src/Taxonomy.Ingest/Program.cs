using System.Text.Json;
using Taxonomy.Ingest;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    switch (args)
    {
        case ["export", var source]:
            await ExportAsync(source, Console.OpenStandardOutput(), cancellation.Token);
            return 0;

        case ["export", var source, var destination]:
            await using (var output = File.Create(destination))
            {
                await ExportAsync(source, output, cancellation.Token);
            }
            return 0;

        default:
            await Console.Error.WriteLineAsync("Usage: taxonomy-ingest export <structure.xml> [output.json]");
            return 2;
    }
}
catch (OperationCanceledException)
{
    return 130;
}

static async Task ExportAsync(string source, Stream output, CancellationToken cancellationToken)
{
    await using var input = File.OpenRead(source);
    var entries = ImageNetXmlParser.Parse(input, cancellationToken);
    await JsonSerializer.SerializeAsync(output, entries, JsonSerializerOptions.Web, cancellationToken);
}
