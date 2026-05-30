using Microsoft.Extensions.Configuration;

namespace Products.Api.Middleware;

public static class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private const string ApiKeyConfigKey = "Authentication:ApiKey";

    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            // Skip auth for OpenAPI/Scalar docs
            if (context.Request.Path.StartsWithSegments("/openapi")
                || context.Request.Path.StartsWithSegments("/scalar"))
            {
                await next();
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header." });
                return;
            }

            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var configuredKey = configuration[ApiKeyConfigKey]
                ?? throw new InvalidOperationException("Authentication:ApiKey is not configured.");

            if (providedKey != configuredKey)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
                return;
            }

            await next();
        });
    }
}
