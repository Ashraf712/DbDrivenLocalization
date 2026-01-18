using DbDrivenLocalization.Interfaces;
using DbDrivenLocalization.Options;
using DbDrivenLocalization.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DbDrivenLocalization.Controllers;

public sealed class CultureController : Controller
{
    private readonly ILanguageService _languageService;
    private readonly DbLocalizationOptions _options;

    public CultureController(ILanguageService languageService, IOptions<DbLocalizationOptions> options)
    {
        _languageService = languageService;
        _options = options.Value;
    }

    [HttpGet("/culture/set")]
    public IActionResult Set(string c, string? returnUrl = "/")
    {
        var allowed = _languageService.GetActiveCultures();
        var culture = allowed.FirstOrDefault(x => x.Equals(c?.Trim(), StringComparison.OrdinalIgnoreCase))
                      ?? _options.DefaultCulture;

        Response.Cookies.Append(
            _options.CultureCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
