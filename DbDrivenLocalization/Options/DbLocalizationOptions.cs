namespace DbDrivenLocalization.Options;

public sealed class DbLocalizationOptions
{
    public string DefaultCulture { get; set; } = "en-US";
    public string CultureCookieName { get; set; } = ".App.Culture";
    public int VersionCheckSeconds { get; set; }
}
