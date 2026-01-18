using DbDrivenLocalization.Models;
using Microsoft.EntityFrameworkCore;

namespace DbDrivenLocalization.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppLanguage> Languages => Set<AppLanguage>();
    public DbSet<AppLanguageResource> LanguageResources => Set<AppLanguageResource>();
    public DbSet<AppLocalizationVersion> LocalizationVersions => Set<AppLocalizationVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppLanguage>(b =>
        {
            b.ToTable("App_Language", "dbo");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Culture).HasMaxLength(20).IsRequired();
            b.Property(x => x.CultureNormalized)
             .HasMaxLength(20)
             .HasComputedColumnSql("LOWER(LTRIM(RTRIM([Culture])))", stored: true);

            b.HasIndex(x => x.CultureNormalized).IsUnique();

            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<AppLanguageResource>(b =>
        {
            b.ToTable("App_LanguageResource", "dbo");
            b.HasKey(x => x.Id);

            b.Property(x => x.ResourceKey).HasMaxLength(200).IsRequired();
            b.Property(x => x.ResourceKeyNormalized)
             .HasMaxLength(200)
             .HasComputedColumnSql("LOWER(LTRIM(RTRIM([ResourceKey])))", stored: true);

            b.HasIndex(x => new { x.LanguageId, x.ResourceKeyNormalized }).IsUnique();

            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            b.HasOne(x => x.Language)
             .WithMany(x => x.Resources)
             .HasForeignKey(x => x.LanguageId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppLocalizationVersion>(b =>
        {
            b.ToTable("App_LocalizationVersion", "dbo");
            b.HasKey(x => x.Id);

            b.Property(x => x.VersionNumber).HasDefaultValue(1);
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
