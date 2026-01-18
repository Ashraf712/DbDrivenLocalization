namespace DbDrivenLocalization.Interfaces;

public interface ILocalizationService
{
    string Get(string resourceKey, params object?[] args);
    string Get(string resourceKey, int languageId, params object?[] args);
    string GetForCulture(string culture, string resourceKey, params object?[] args);
}
