using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class LoanApplicationParty : Entity
{
    public Guid LoanApplicationId { get; private set; }
    public PartyType PartyType { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? BVN { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Designation { get; private set; }
    public decimal? ShareholdingPercent { get; private set; }
    public bool BVNVerified { get; private set; }
    public DateTime? BVNVerifiedAt { get; private set; }
    public string? Address { get; private set; }
    public DateTime? DateOfBirth { get; private set; }

    private LoanApplicationParty() { }

    public static Result<LoanApplicationParty> Create(
        Guid loanApplicationId,
        PartyType partyType,
        string fullName,
        string? bvn,
        string? email,
        string? phoneNumber,
        string? designation,
        decimal? shareholdingPercent)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<LoanApplicationParty>("Full name is required");

        if (shareholdingPercent.HasValue && (shareholdingPercent < 0 || shareholdingPercent > 100))
            return Result.Failure<LoanApplicationParty>("Shareholding must be between 0 and 100");

        return Result.Success(new LoanApplicationParty
        {
            LoanApplicationId = loanApplicationId,
            PartyType = partyType,
            FullName = fullName,
            BVN = bvn,
            Email = email,
            PhoneNumber = phoneNumber,
            Designation = designation,
            ShareholdingPercent = shareholdingPercent
        });
    }

    public void MarkBVNVerified()
    {
        BVNVerified = true;
        BVNVerifiedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string? address, DateTime? dateOfBirth)
    {
        Address = address;
        DateOfBirth = dateOfBirth;
    }

    public void UpdateBVN(string bvn) { BVN = bvn; }

    public void UpdateShareholdingPercent(decimal pct) { ShareholdingPercent = pct; }
}
