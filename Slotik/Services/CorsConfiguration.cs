namespace Slotik.Services;

public static class CorsConfiguration
{
    public static IServiceCollection AddSlotikCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
                || origin != uri.GetLeftPart(UriPartial.Authority) || origin.Contains('*'))
                throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTP/HTTPS origins without paths or wildcards.");
        }
        var allowed = new HashSet<string>(origins, StringComparer.Ordinal);
        services.AddCors(options => options.AddPolicy("front", policy => policy
            .SetIsOriginAllowed(allowed.Contains).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }
}
