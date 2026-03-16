using CRMS.Domain.Aggregates.Location;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Location;

public class LocationConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Location.Location>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Location.Location> builder)
    {
        builder.ToTable("Locations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.ManagerName)
            .HasMaxLength(100);

        builder.Property(x => x.ContactPhone)
            .HasMaxLength(20);

        builder.Property(x => x.ContactEmail)
            .HasMaxLength(100);

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0);

        // Self-referencing relationship for hierarchy
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => x.Type);

        builder.HasIndex(x => x.ParentLocationId);

        builder.HasIndex(x => x.IsActive);

        // Composite index for common queries
        builder.HasIndex(x => new { x.Type, x.IsActive });
    }
}
