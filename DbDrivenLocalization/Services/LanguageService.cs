using DbDrivenLocalization.Interfaces;
using DbDrivenLocalization.Models;

namespace DbDrivenLocalization.Services;

public sealed class LanguageService : ILanguageService
{
    private readonly LocalizationCacheStore _store;

    public LanguageService(LocalizationCacheStore store)
    {
        _store = store;
    }

    public IReadOnlyList<AppLanguage> GetActiveLanguages() => _store.GetActiveLanguages();

    public IReadOnlyList<string> GetActiveCultures() => _store.GetActiveCultures();

    public AppLanguage? GetByCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return null;

        var normalized = culture.Trim();
        return _store.GetActiveLanguages()
            .FirstOrDefault(x => x.Culture.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
