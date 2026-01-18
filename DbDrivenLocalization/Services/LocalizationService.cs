using DbDrivenLocalization.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DbDrivenLocalization.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly LocalizationCacheStore _store;
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(LocalizationCacheStore store, ILogger<LocalizationService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public string Get(string resourceKey, params object?[] args)
        => GetForCulture(CultureInfo.CurrentUICulture.Name, resourceKey, args);

    public string Get(string resourceKey, int languageId, params object?[] args)
    {
        var langs = _store.GetActiveLanguages();
        var lang = langs.FirstOrDefault(x => x.Id == languageId);
        var culture = lang?.Culture ?? CultureInfo.CurrentUICulture.Name;
        return GetForCulture(culture, resourceKey, args);
    }

    public string GetForCulture(string culture, string resourceKey, params object?[] args)
    {
        try
        {
            if (_store.TryGetValue(culture, resourceKey, out var value) && !string.IsNullOrEmpty(value))
                return FormatIfNeeded(value!, args);
            return resourceKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Localization lookup failed for key '{Key}' culture '{Culture}'", resourceKey, culture);
            return resourceKey;
        }
    }

    private static string FormatIfNeeded(string template, object?[]? args)
    {
        if (args is null || args.Length == 0) return template;
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
