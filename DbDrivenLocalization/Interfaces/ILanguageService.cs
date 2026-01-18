using DbDrivenLocalization.Models;

namespace DbDrivenLocalization.Interfaces;

public interface ILanguageService
{
    IReadOnlyList<AppLanguage> GetActiveLanguages();
    AppLanguage? GetByCulture(string culture);
    IReadOnlyList<string> GetActiveCultures();
}
