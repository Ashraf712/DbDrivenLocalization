using DbDrivenLocalization.Services;

namespace DbDrivenLocalization.Infrastructure;

/// <summary>
/// Calls EnsureFreshAsync once per request, but DB check is TTL-gated (e.g., 60s).
/// </summary>
public sealed class LocalizationVersionMiddleware
{
    private readonly RequestDelegate _next;

    public LocalizationVersionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, LocalizationCacheStore store)
    {
        // Lightweight check (mostly memory). Reload only when DB version changes.
        await store.EnsureFreshAsync(context.RequestAborted);
        await _next(context);
    }
}
