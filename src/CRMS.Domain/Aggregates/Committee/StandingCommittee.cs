using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Committee;

public class StandingCommittee : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public CommitteeType CommitteeType { get; private set; }
    public int RequiredVotes { get; private set; }
    public int MinimumApprovalVotes { get; private set; }
    public int DefaultDeadlineHours { get; private set; }

    public decimal MinAmountThreshold { get; private set; }
    public decimal? MaxAmountThreshold { get; private set; }

    public bool IsActive { get; private set; }

    private readonly List<StandingCommitteeMember> _members = [];
    public IReadOnlyCollection<StandingCommitteeMember> Members => _members.AsReadOnly();

    private StandingCommittee() { }

    public static Result<StandingCommittee> Create(
        string name,
        CommitteeType committeeType,
        int requiredVotes,
        int minimumApprovalVotes,
        int defaultDeadlineHours,
        decimal minAmountThreshold,
        decimal? maxAmountThreshold)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<StandingCommittee>("Committee name is required");

        if (requiredVotes <= 0)
            return Result.Failure<StandingCommittee>("Required votes must be greater than zero");

        if (minimumApprovalVotes <= 0 || minimumApprovalVotes > requiredVotes)
            return Result.Failure<StandingCommittee>("Minimum approval votes must be between 1 and required votes");

        if (minAmountThreshold < 0)
            return Result.Failure<StandingCommittee>("Minimum amount threshold cannot be negative");

        if (maxAmountThreshold.HasValue && maxAmountThreshold.Value <= minAmountThreshold)
            return Result.Failure<StandingCommittee>("Maximum amount must be greater than minimum amount");

        return Result.Success(new StandingCommittee
        {
            Name = name,
            CommitteeType = committeeType,
            RequiredVotes = requiredVotes,
            MinimumApprovalVotes = minimumApprovalVotes,
            DefaultDeadlineHours = defaultDeadlineHours,
            MinAmountThreshold = minAmountThreshold,
            MaxAmountThreshold = maxAmountThreshold,
            IsActive = true
        });
    }

    public Result Update(
        string name,
        int requiredVotes,
        int minimumApprovalVotes,
        int defaultDeadlineHours,
        decimal minAmountThreshold,
        decimal? maxAmountThreshold)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Committee name is required");

        if (requiredVotes <= 0)
            return Result.Failure("Required votes must be greater than zero");

        if (minimumApprovalVotes <= 0 || minimumApprovalVotes > requiredVotes)
            return Result.Failure("Minimum approval votes must be between 1 and required votes");

        Name = name;
        RequiredVotes = requiredVotes;
        MinimumApprovalVotes = minimumApprovalVotes;
        DefaultDeadlineHours = defaultDeadlineHours;
        MinAmountThreshold = minAmountThreshold;
        MaxAmountThreshold = maxAmountThreshold;
        return Result.Success();
    }

    public Result AddMember(Guid userId, string userName, string role, bool isChairperson)
    {
        if (_members.Any(m => m.UserId == userId))
            return Result.Failure("User is already a member of this committee");

        if (isChairperson && _members.Any(m => m.IsChairperson))
        {
            foreach (var m in _members.Where(m => m.IsChairperson))
                m.SetChairperson(false);
        }

        var member = StandingCommitteeMember.Create(Id, userId, userName, role, isChairperson);
        if (member.IsFailure)
            return Result.Failure(member.Error);

        _members.Add(member.Value);
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result.Failure("Member not found");

        _members.Remove(member);
        return Result.Success();
    }

    public Result UpdateMember(Guid userId, string role, bool isChairperson)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result.Failure("Member not found");

        if (isChairperson)
        {
            foreach (var m in _members.Where(m => m.IsChairperson && m.UserId != userId))
                m.SetChairperson(false);
        }

        member.UpdateRole(role);
        member.SetChairperson(isChairperson);
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

public class StandingCommitteeMember : Entity
{
    public Guid StandingCommitteeId { get; private set; }
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsChairperson { get; private set; }

    private StandingCommitteeMember() { }

    internal static Result<StandingCommitteeMember> Create(
        Guid standingCommitteeId, Guid userId, string userName, string role, bool isChairperson)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Result.Failure<StandingCommitteeMember>("User name is required");
        if (string.IsNullOrWhiteSpace(role))
            return Result.Failure<StandingCommitteeMember>("Role is required");

        return Result.Success(new StandingCommitteeMember
        {
            StandingCommitteeId = standingCommitteeId,
            UserId = userId,
            UserName = userName,
            Role = role,
            IsChairperson = isChairperson
        });
    }

    internal void UpdateRole(string role) => Role = role;
    internal void SetChairperson(bool value) => IsChairperson = value;
}
