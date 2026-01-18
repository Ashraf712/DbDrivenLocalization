using DbDrivenLocalization.Interfaces;
using DbDrivenLocalization.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using System.Globalization;

namespace DbDrivenLocalization.Models;

public abstract class CustomBaseViewPage<TModel> : Microsoft.AspNetCore.Mvc.Razor.RazorPage<TModel>
{
    [RazorInject]
    public ILocalizationService LocalizationService { get; set; } = default!;

    public delegate string Localizer(string resourceKey, params object?[] args);
    private Localizer? _localizer;

    public Localizer Localize
    {
        get
        {
            _localizer ??= (resourceKey, args) =>
                LocalizationService.GetForCulture(CultureInfo.CurrentUICulture.Name, resourceKey, args ?? Array.Empty<object?>());
            return _localizer;
        }
    }
    public IHtmlContent LocalizeRaw(string resourceKey, params object?[] args)
        => new HtmlString(LocalizationService.GetForCulture(CultureInfo.CurrentUICulture.Name, resourceKey, args ?? Array.Empty<object?>()));
}

public abstract class CustomBaseViewPage : CustomBaseViewPage<dynamic> { }
