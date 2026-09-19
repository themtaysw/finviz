namespace Taxonomy.Api.Infrastructure;

internal static class WebAppHosting
{
    public static WebApplication UseWebApp(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Vite fingerprints everything under /assets; index.html has to be revalidated.
                context.Context.Response.Headers.CacheControl =
                    context.Context.Request.Path.StartsWithSegments("/assets")
                        ? "public, max-age=31536000, immutable"
                        : "no-cache";
            },
        });

        return app;
    }
}
