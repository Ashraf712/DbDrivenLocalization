namespace DbDrivenLocalization.Models;

public sealed class AppLanguageResource
{
    public long Id { get; set; }
    public int LanguageId { get; set; }
    public string ResourceKey { get; set; } = "";
    public string ResourceKeyNormalized { get; private set; } = ""; 
    public string Value { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public AppLanguage Language { get; set; } = null!;
}
