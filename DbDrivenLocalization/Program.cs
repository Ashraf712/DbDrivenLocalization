using DbDrivenLocalization.Data;
using DbDrivenLocalization.Infrastructure;
using DbDrivenLocalization.Interfaces;
using DbDrivenLocalization.Options;
using DbDrivenLocalization.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.Configure<DbLocalizationOptions>(builder.Configuration.GetSection("DbLocalization"));
builder.Services.AddMemoryCache();
builder.Services.AddDbContextFactory<AppDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddSingleton<LocalizationCacheStore>();
builder.Services.AddSingleton<ILanguageService, LanguageService>();
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
builder.Services.AddHostedService<LocalizationWarmupHostedService>();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var store = scope.ServiceProvider.GetRequiredService<LocalizationCacheStore>();
    await store.PreloadAsync();
        var cultures = store.GetActiveCultures();
    var supportedCultures = cultures
        .Select(c =>
        {
            try { return CultureInfo.GetCultureInfo(c); }
            catch { return null; }
        })
        .Where(x => x != null)
        .Cast<CultureInfo>()
        .ToList();
    if (supportedCultures.Count == 0)
        supportedCultures.Add(CultureInfo.GetCultureInfo("en-US"));
    var locOptions = app.Services.GetRequiredService<IOptions<DbLocalizationOptions>>().Value;
    var requestLocalizationOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(locOptions.DefaultCulture),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures
    };
    requestLocalizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
    {
        CookieName = locOptions.CultureCookieName
    });
    app.UseRequestLocalization(requestLocalizationOptions);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<LocalizationVersionMiddleware>();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.Run();
