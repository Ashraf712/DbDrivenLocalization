namespace DbDrivenLocalization.Models;

public sealed class AppLanguage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Culture { get; set; } = "";
    public string CultureNormalized { get; private set; } = ""; 
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public ICollection<AppLanguageResource> Resources { get; set; } = new List<AppLanguageResource>();
}
