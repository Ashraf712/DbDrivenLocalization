using DbDrivenLocalization.Services;

namespace DbDrivenLocalization.Infrastructure;

public sealed class LocalizationWarmupHostedService : IHostedService
{
    private readonly LocalizationCacheStore _store;

    public LocalizationWarmupHostedService(LocalizationCacheStore store)
    {
        _store = store;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => _store.PreloadAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
