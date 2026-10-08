using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Models;

namespace Wissen.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Seite> Seiten => Set<Seite>();

    public DbSet<SeitenVersion> SeitenVersionen => Set<SeitenVersion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Seite>(seite =>
        {
            seite.ToTable("Seiten");
            seite.Property(s => s.Path).HasMaxLength(Seite.MaxPathLength);
            seite.Property(s => s.Kategorie).HasMaxLength(200);
            seite.HasIndex(s => s.Path).IsUnique();
            seite.HasIndex(s => s.Kategorie);
        });

        builder.Entity<SeitenVersion>(version =>
        {
            version.ToTable("SeitenVersionen");
            version.Property(v => v.Kategorie).HasMaxLength(200);
            version.Property(v => v.Autor).HasMaxLength(256);
            version.HasIndex(v => new { v.SeiteId, v.Nummer }).IsUnique();
            version.HasOne(v => v.Seite)
                .WithMany(s => s.Versionen)
                .HasForeignKey(v => v.SeiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
