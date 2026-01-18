using DbDrivenLocalization.Data;
using DbDrivenLocalization.Models;
using DbDrivenLocalization.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DbDrivenLocalization.Services;

public sealed class LocalizationCacheStore
{
    private sealed class Snapshot
    {
        public required long Version { get; init; }
        public required string DefaultCulture { get; init; }

        public required IReadOnlyList<AppLanguage> ActiveLanguages { get; init; }
        public required Dictionary<string, int> CultureToLanguageId { get; init; }
        public required Dictionary<string, Dictionary<string, string>> ResourcesByCulture { get; init; }
    }

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LocalizationCacheStore> _logger;
    private readonly DbLocalizationOptions _options;

    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private volatile Snapshot _snapshot;

    private const string VersionCheckCacheKey = "DbLoc:VersionCheck";

    public LocalizationCacheStore(
        IDbContextFactory<AppDbContext> dbFactory,
        IMemoryCache memoryCache,
        IOptions<DbLocalizationOptions> options,
        ILogger<LocalizationCacheStore> logger)
    {
        _dbFactory = dbFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;

        _snapshot = new Snapshot
        {
            Version = 0,
            DefaultCulture = _options.DefaultCulture,
            ActiveLanguages = Array.Empty<AppLanguage>(),
            CultureToLanguageId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ResourcesByCulture = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        };
    }

    public IReadOnlyList<AppLanguage> GetActiveLanguages() => _snapshot.ActiveLanguages;
    public IReadOnlyList<string> GetActiveCultures() => _snapshot.ActiveLanguages.Select(x => x.Culture).ToList();

    public int? TryGetLanguageId(string culture)
    {
        var snap = _snapshot;
        if (string.IsNullOrWhiteSpace(culture)) return null;

        culture = NormalizeCulture(culture);

        if (snap.CultureToLanguageId.TryGetValue(culture, out var id))
            return id;

        var parent = TryGetParentCulture(culture);
        if (parent is not null && snap.CultureToLanguageId.TryGetValue(parent, out id))
            return id;

        return null;
    }

    public bool TryGetValue(string culture, string resourceKey, out string? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(resourceKey))
            return false;

        var snap = _snapshot;

        culture = NormalizeCulture(string.IsNullOrWhiteSpace(culture) ? snap.DefaultCulture : culture);

        if (!snap.ResourcesByCulture.TryGetValue(culture, out var dict))
        {
            var parent = TryGetParentCulture(culture);
            if (parent is not null)
                snap.ResourcesByCulture.TryGetValue(parent, out dict);
        }

        dict ??= snap.ResourcesByCulture.TryGetValue(snap.DefaultCulture, out var def) ? def : null;

        if (dict is null)
            return false;

        var k = NormalizeKey(resourceKey);
        if (dict.TryGetValue(k, out var v))
        {
            value = v;
            return true;
        }

        if (!culture.Equals(snap.DefaultCulture, StringComparison.OrdinalIgnoreCase)
    && snap.ResourcesByCulture.TryGetValue(snap.DefaultCulture, out var defDict)
    && defDict.TryGetValue(k, out v))
        {
            value = v;
            return true;
        }

        return false;
    }

    public async Task EnsureFreshAsync(CancellationToken ct = default)
    {
        if (_memoryCache.TryGetValue(VersionCheckCacheKey, out _))
            return;

        var ttl = TimeSpan.FromSeconds(Math.Max(2, _options.VersionCheckSeconds));
        _memoryCache.Set(VersionCheckCacheKey, true, ttl);

        try
        {
            var dbVersion = await ReadDbVersionAsync(ct).ConfigureAwait(false);
            if (dbVersion == _snapshot.Version)
                return;

            await ReloadAsync(dbVersion, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Localization version check failed. Serving existing cache.");
        }
    }

    public async Task PreloadAsync(CancellationToken ct = default)
    {
        try
        {
            var dbVersion = await ReadDbVersionAsync(ct).ConfigureAwait(false);
            await ReloadAsync(dbVersion, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Localization preload failed. App will fallback to showing keys.");
        }
    }

    private async Task<long> ReadDbVersionAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var v = await db.LocalizationVersions
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.VersionNumber)
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return v;
    }

    private async Task ReloadAsync(long dbVersion, CancellationToken ct)
    {
        await _reloadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (dbVersion == _snapshot.Version)
                return;

            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var languages = await db.Languages
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var cultureToLangId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in languages)
            {
                var culture = NormalizeCulture(l.Culture);
                cultureToLangId[culture] = l.Id;
            }

            var rows = await (from r in db.LanguageResources.AsNoTracking()
                                join l in db.Languages.AsNoTracking() on r.LanguageId equals l.Id
                                where r.IsActive && l.IsActive
                                select new { l.Culture, r.ResourceKey, r.Value }
                            ).ToListAsync(ct).ConfigureAwait(false);

            var resourcesByCulture = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var culture = NormalizeCulture(row.Culture);
                if (!resourcesByCulture.TryGetValue(culture, out var dict))
                {
                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    resourcesByCulture[culture] = dict;
                }

                var key = NormalizeKey(row.ResourceKey);
                dict[key] = row.Value;
            }

            if (!resourcesByCulture.ContainsKey(_options.DefaultCulture))
                resourcesByCulture[_options.DefaultCulture] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var newSnapshot = new Snapshot
            {
                Version = dbVersion,
                DefaultCulture = _options.DefaultCulture,
                ActiveLanguages = languages,
                CultureToLanguageId = cultureToLangId,
                ResourcesByCulture = resourcesByCulture
            };

            _snapshot = newSnapshot;

            _logger.LogInformation("Localization cache reloaded. Version={Version}, Languages={LangCount}, Rows={RowCount}",
                dbVersion, languages.Count, rows.Count);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (char.IsWhiteSpace(key[0]) || char.IsWhiteSpace(key[^1]))
            return key.Trim();
        return key;
    }

    private static string NormalizeCulture(string culture)
    {
        if (string.IsNullOrEmpty(culture)) return culture;
        if (char.IsWhiteSpace(culture[0]) || char.IsWhiteSpace(culture[^1]))
            culture = culture.Trim();
        return culture;
    }

    private static string? TryGetParentCulture(string culture)
    {
        try
        {
            var ci = CultureInfo.GetCultureInfo(culture);
            if (!string.IsNullOrWhiteSpace(ci.Parent?.Name))
                return ci.Parent.Name;
        }
        catch { /* ignore invalid culture */ }
        return null;
    }
}
