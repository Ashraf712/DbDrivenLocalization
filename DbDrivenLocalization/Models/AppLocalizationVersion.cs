namespace DbDrivenLocalization.Models;

public sealed class AppLocalizationVersion
{
    public int Id { get; set; }  
    public long VersionNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
}
