using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampApplicationConfiguration : IEntityTypeConfiguration<NampApplication>
{
    public void Configure(EntityTypeBuilder<NampApplication> builder)
    {
        builder.ToTable("NampApplications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ApplicationReference)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.LoanProductId)
            .IsRequired();

        builder.Property(x => x.ApplicantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.BoaAccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ApplicantPhone)
            .HasMaxLength(30);

        builder.Property(x => x.ApplicantEmail)
            .HasMaxLength(200);

        builder.Property(x => x.ApplicantCategory)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.EquipmentDescription)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.EquipmentValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.EquipmentCartJson)
            .HasColumnType("longtext");

        // Extended applicant info
        builder.Property(x => x.Nin).HasMaxLength(20);
        builder.Property(x => x.Bvn).HasMaxLength(20);
        builder.Property(x => x.BoaAccountName).HasMaxLength(200);
        builder.Property(x => x.LoanPurpose).HasColumnType("text");
        builder.Property(x => x.IndustrySector).HasMaxLength(200);
        builder.Property(x => x.DateOfBirth).HasMaxLength(20);
        builder.Property(x => x.StateOfResidence).HasMaxLength(100);
        builder.Property(x => x.LocalGovernmentArea).HasMaxLength(100);
        builder.Property(x => x.CompanyName).HasMaxLength(300);
        builder.Property(x => x.RcNumber).HasMaxLength(50);

        // CAC company profile (Agro-Service)
        builder.Property(x => x.CacStatus).HasMaxLength(100);
        builder.Property(x => x.CacEntityType).HasMaxLength(100);
        builder.Property(x => x.CacRegistrationDate).HasMaxLength(50);
        builder.Property(x => x.CacNatureOfBusiness).HasMaxLength(500);
        builder.Property(x => x.CacShareCapital).HasColumnType("decimal(18,2)");
        builder.Property(x => x.CacCompanyId);
        builder.Property(x => x.CacAddress).HasMaxLength(500);
        builder.Property(x => x.CacCity).HasMaxLength(100);
        builder.Property(x => x.CacState).HasMaxLength(100);
        builder.Property(x => x.CacRawJson).HasColumnType("longtext");

        // Individual credit profile
        builder.Property(x => x.Occupation).HasMaxLength(200);
        builder.Property(x => x.EmployerName).HasMaxLength(300);
        builder.Property(x => x.EmploymentStatus).HasMaxLength(50);
        builder.Property(x => x.OtherIncomeSource).HasMaxLength(500);
        builder.Property(x => x.OwnedAssetsDescription).HasColumnType("text");
        builder.Property(x => x.ExistingLoanObligations).HasColumnType("text");
        builder.Property(x => x.EstimatedMonthlyIncome).HasColumnType("decimal(18,2)");
        builder.Property(x => x.OtherMonthlyIncome).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MonthlyLivingExpenses).HasColumnType("decimal(18,2)");
        builder.Property(x => x.EstimatedNetWorth).HasColumnType("decimal(18,2)");

        // Equity & loan amounts
        builder.Property(x => x.EquityPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.CartTotal).HasColumnType("decimal(18,2)");
        builder.Property(x => x.EquityAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LoanAmount).HasColumnType("decimal(18,2)");

        builder.Property(x => x.CommitteeTier)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.FinancialAppraisalNote)
            .HasColumnType("text");

        builder.Property(x => x.CommitteeDecisionNote)
            .HasColumnType("text");

        builder.Property(x => x.RatificationDeclineNote)
            .HasColumnType("text");

        builder.Property(x => x.OfferLetterStoragePath)
            .HasMaxLength(500);

        builder.Property(x => x.PreDeploymentNote).HasColumnType("text");

        builder.Property(x => x.DeploymentNote)
            .HasColumnType("text");

        // Fineract integration
        builder.Property(x => x.FineractClientId);
        builder.Property(x => x.ApprovedInterestRate).HasColumnType("decimal(10,4)");
        builder.Property(x => x.FineractLoanId);
        builder.Property(x => x.FineractLoanAccountNumber).HasMaxLength(100);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        // Collections
        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(d => d.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Guarantors)
            .WithOne()
            .HasForeignKey(g => g.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Collaterals)
            .WithOne()
            .HasForeignKey(c => c.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.FinancialStatements)
            .WithOne()
            .HasForeignKey(f => f.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.PreDeploymentChecklist)
            .WithOne()
            .HasForeignKey(i => i.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Directors)
            .WithOne()
            .HasForeignKey(d => d.NampApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ApplicationNumber).IsUnique();
        builder.HasIndex(x => x.ApplicationReference).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => new { x.Status, x.BranchId });
    }
}
