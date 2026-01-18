using DbDrivenLocalization.Interfaces;
using DbDrivenLocalization.Services;
using Microsoft.AspNetCore.Mvc;

namespace DbDrivenLocalization.ViewComponents;

public sealed class LanguageSwitcherViewComponent : ViewComponent
{
    private readonly ILanguageService _languageService;

    public LanguageSwitcherViewComponent(ILanguageService languageService)
    {
        _languageService = languageService;
    }

    public IViewComponentResult Invoke()
    {
        var langs = _languageService.GetActiveLanguages();
        return View(langs);
    }
}
