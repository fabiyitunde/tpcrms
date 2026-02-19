using CRMS.Domain.Aggregates.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Workflow;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.ApplicationType, x.IsActive });

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.ApplicationType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(x => x.Stages)
            .WithOne()
            .HasForeignKey(x => x.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(x => x.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class WorkflowStageConfiguration : IEntityTypeConfiguration<WorkflowStage>
{
    public void Configure(EntityTypeBuilder<WorkflowStage> builder)
    {
        builder.ToTable("WorkflowStages");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Status }).IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.AssignedRole)
            .HasMaxLength(50)
            .IsRequired();
    }
}

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStatus, x.ToStatus, x.Action });

        builder.Property(x => x.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Action)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.RequiredRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ConditionExpression)
            .HasMaxLength(500);
    }
}

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.LoanApplicationId).IsUnique();
        builder.HasIndex(x => x.CurrentStatus);
        builder.HasIndex(x => x.AssignedRole);
        builder.HasIndex(x => x.AssignedToUserId);
        builder.HasIndex(x => new { x.IsCompleted, x.SLADueAt });

        builder.Property(x => x.CurrentStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.CurrentStageDisplayName)
            .HasMaxLength(100);

        builder.Property(x => x.AssignedRole)
            .HasMaxLength(50);

        builder.Property(x => x.FinalStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Concurrency token disabled for MySQL compatibility
        // RowVersion stored as BLOB, must have default value to avoid DBNull issues
        builder.Property(x => x.RowVersion)
            .HasColumnType("BLOB")
            .HasDefaultValue(new byte[] { 0 })
            .IsRequired(false);

        builder.HasMany(x => x.TransitionHistory)
            .WithOne()
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class WorkflowTransitionLogConfiguration : IEntityTypeConfiguration<WorkflowTransitionLog>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionLog> builder)
    {
        builder.ToTable("WorkflowTransitionLogs");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.WorkflowInstanceId);
        builder.HasIndex(x => x.PerformedAt);

        builder.Property(x => x.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Action)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Comment)
            .HasMaxLength(1000);
    }
}
