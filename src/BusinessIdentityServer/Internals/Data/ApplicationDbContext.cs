using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BusinessIdentityServer.Internals.Models.DatabaseModels;

namespace BusinessIdentityServer.Internals.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private const string CheckConstraintPrefix = "CK";
    private const string NotWhitespaceRule = "NotWhitespace";

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {}

    public DbSet<EmailRecord> EmailRecords { get; set; }
    public DbSet<UserAccessRecord> UserAccessRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);

        // configure ApplicationUser entity constraints and database-level invariants
        builder.Entity<ApplicationUser>(b =>
        {
            // resolve table name and property dynamically to construct a deterministic constraint name
            string tableName = b.Metadata.GetTableName() ?? "AspNetUsers";
            string propertyName = nameof(ApplicationUser.Email);

            // enforce schema-level NOT NULL and 256-character maximum length matching ASP.NET Core Identity standards
            b.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            // enforce a database-level check constraint ensuring Email contains non-whitespace characters
            b.ToTable(t => t.HasCheckConstraint(
                $"{CheckConstraintPrefix}_{tableName}_{propertyName}_{NotWhitespaceRule}",
                $"length(trim(\"{propertyName}\")) > 0"
            ));
        });
    }
}
