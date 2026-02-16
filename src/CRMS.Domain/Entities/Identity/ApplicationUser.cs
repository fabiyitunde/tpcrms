using CRMS.Domain.Common;

namespace CRMS.Domain.Entities.Identity;

public class ApplicationUser : Entity
{
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string NormalizedUserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public UserType Type { get; private set; }
    public UserStatus Status { get; private set; }
    public Guid? BranchId { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }
    public string? SecurityStamp { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }

    private readonly List<ApplicationUserRole> _userRoles = [];
    public IReadOnlyCollection<ApplicationUserRole> UserRoles => _userRoles.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    private ApplicationUser() { }

    public static Result<ApplicationUser> Create(
        string email,
        string userName,
        string firstName,
        string lastName,
        UserType type,
        string? phoneNumber = null,
        Guid? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<ApplicationUser>("Email is required");

        if (string.IsNullOrWhiteSpace(userName))
            return Result.Failure<ApplicationUser>("Username is required");

        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure<ApplicationUser>("First name is required");

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure<ApplicationUser>("Last name is required");

        return Result.Success(new ApplicationUser
        {
            Email = email.ToLowerInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Type = type,
            Status = UserStatus.Active,
            BranchId = branchId,
            SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void UpdateRefreshToken(string refreshToken, DateTime expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndAt = null;
    }

    public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndAt = DateTime.UtcNow.Add(lockoutDuration);
            Status = UserStatus.Locked;
        }
    }

    public bool IsLockedOut => LockoutEndAt.HasValue && LockoutEndAt > DateTime.UtcNow;

    public Result Activate()
    {
        if (Status == UserStatus.Active)
            return Result.Failure("User is already active");

        Status = UserStatus.Active;
        LockoutEndAt = null;
        FailedLoginAttempts = 0;
        return Result.Success();
    }

    public Result Deactivate()
    {
        Status = UserStatus.Inactive;
        return Result.Success();
    }

    public Result Lock(DateTime? until = null)
    {
        Status = UserStatus.Locked;
        LockoutEndAt = until ?? DateTime.UtcNow.AddYears(100);
        return Result.Success();
    }

    public void AddRole(ApplicationRole role)
    {
        if (_userRoles.Any(ur => ur.RoleId == role.Id))
            return;

        _userRoles.Add(new ApplicationUserRole(Id, role.Id));
    }

    public void RemoveRole(Guid roleId)
    {
        var userRole = _userRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (userRole != null)
            _userRoles.Remove(userRole);
    }

    public Result UpdateProfile(string firstName, string lastName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure("First name is required");

        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure("Last name is required");

        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        return Result.Success();
    }
}

public enum UserType
{
    Staff,
    Customer
}

public enum UserStatus
{
    Active,
    Inactive,
    Locked,
    PendingVerification
}
